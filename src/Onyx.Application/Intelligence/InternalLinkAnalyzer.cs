using System.Text;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Content;
using Onyx.Contracts.Intelligence;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

/// <summary>
/// Rule-based internal-link analysis over live content: broken links, orphan pages (no inbound links from other live pages),
/// keyword-based link opportunities, ambiguous repeated anchors and excessive links. Evidence is the site's own content.
/// Proposed edits to published pages go through recommendation review; nothing is changed automatically.
/// </summary>
public sealed class InternalLinkAnalyzer(
    IContentRepository content,
    IContentRevisionRepository revisions,
    IContentPathRepository paths,
    ISeoRecommendationRepository recommendations,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IInternalLinkAnalyzer
{
    public const int MaxSuggestions = 200;

    public async Task<InternalLinkReportResponse> AnalyzeAsync(CancellationToken cancellationToken)
    {
        var graph = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var broken = new List<LinkIssueResponse>();
        var excessive = new List<LinkIssueResponse>();
        var inbound = new HashSet<(Guid, string)>();
        var anchors = new Dictionary<(string Language, string Anchor), (HashSet<Guid> Targets, int Count)>();

        foreach (var page in graph.Pages)
        {
            var links = page.Links;
            if (links.Count > ContentQualityGate.ExcessiveLinksPerPage)
            {
                excessive.Add(new LinkIssueResponse(page.Id, page.Language, string.Empty, string.Empty, $"{links.Count} links on one page."));
            }

            foreach (var (anchor, link) in links)
            {
                if (link.ContentItemId is not { } target)
                {
                    continue;
                }

                if (!graph.LiveIds.Contains(target))
                {
                    broken.Add(new LinkIssueResponse(page.Id, page.Language, anchor, target.ToString(), graph.KnownIds.Contains(target) ? "Target is not live" : "Target no longer exists"));
                    continue;
                }

                if (target != page.Id)
                {
                    inbound.Add((target, page.Language));
                }

                var key = (page.Language, Normalize(anchor));
                var entry = anchors.TryGetValue(key, out var existing) ? existing : (Targets: new HashSet<Guid>(), Count: 0);
                entry.Targets.Add(target);
                anchors[key] = (entry.Targets, entry.Count + 1);
            }
        }

        var orphans = graph.Pages
            .Where(page => !inbound.Contains((page.Id, page.Language)))
            .Select(page => new OrphanContentResponse(page.Id, page.Language, page.Title, page.Path))
            .ToList();

        var suggestions = new List<LinkSuggestionResponse>();
        foreach (var source in graph.Pages)
        {
            var linkedTargets = source.Links.Select(link => link.Link.ContentItemId).OfType<Guid>().ToHashSet();
            foreach (var target in graph.Pages.Where(target => target.Language == source.Language && target.Id != source.Id && target.Keyword.Length > 0))
            {
                if (suggestions.Count >= MaxSuggestions)
                {
                    break;
                }

                if (!linkedTargets.Contains(target.Id) && source.NormalizedText.Contains($" {target.Keyword} ", StringComparison.Ordinal))
                {
                    suggestions.Add(new LinkSuggestionResponse(source.Id, target.Id, source.Language, target.RawKeyword,
                        $"The text mentions \"{target.RawKeyword}\", the primary keyword of \"{target.Title}\", without linking to it."));
                }
            }
        }

        var repeated = anchors
            .Where(entry => entry.Value.Targets.Count > 1 && entry.Key.Anchor.Length > 0)
            .Select(entry => new RepeatedAnchorResponse(entry.Key.Language, entry.Key.Anchor, entry.Value.Targets.Count, entry.Value.Count))
            .ToList();

        return new InternalLinkReportResponse(broken, orphans, suggestions, repeated, excessive);
    }

    public async Task<GeneratedRecommendationsResponse> GenerateRecommendationsAsync(CancellationToken cancellationToken)
    {
        var report = await AnalyzeAsync(cancellationToken).ConfigureAwait(false);
        var created = 0;
        var skipped = 0;

        async Task ProposeAsync(RecommendationKind kind, Guid contentId, string language, string title, string issue, IReadOnlyList<ProposedFieldChange> changes)
        {
            if (await recommendations.OpenExistsAsync(kind, contentId, language, title, cancellationToken).ConfigureAwait(false))
            {
                skipped++;
                return;
            }

            var evidence = new EvidenceItem(EvidenceSource.RuleEngine, issue, timeProvider.GetUtcNow());
            var proposed = SeoRecommendation.Propose(kind, RecommendationOrigin.RuleEngine, RecommendationBasis.Evidence, title, issue,
                "Detected by Onyx's internal-link rules over the live site.", changes, [evidence], contentId, language, null);
            if (proposed.IsSuccess)
            {
                recommendations.Add(proposed.Value);
                created++;
            }
        }

        foreach (var issue in report.BrokenLinks)
        {
            await ProposeAsync(RecommendationKind.BrokenLink, issue.ContentItemId, issue.LanguageCode, $"Fix link \"{Short(issue.AnchorText)}\"",
                $"The link \"{issue.AnchorText}\" points to content that is not available ({issue.Problem}).", []).ConfigureAwait(false);
        }

        foreach (var orphan in report.Orphans)
        {
            await ProposeAsync(RecommendationKind.OrphanContent, orphan.ContentItemId, orphan.LanguageCode, $"Link to \"{Short(orphan.Title)}\" from related pages",
                "No other live page links to this page in this language, so visitors and search engines struggle to find it.", []).ConfigureAwait(false);
        }

        foreach (var suggestion in report.Suggestions)
        {
            await ProposeAsync(RecommendationKind.InternalLink, suggestion.FromContentItemId, suggestion.LanguageCode,
                $"Consider linking \"{Short(suggestion.AnchorText)}\"", suggestion.Reason,
                [new ProposedFieldChange("body", null, $"Link \"{suggestion.AnchorText}\" to content {suggestion.ToContentItemId:D}")]).ConfigureAwait(false);
        }

        if (created > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new GeneratedRecommendationsResponse(created, skipped);
    }

    private async Task<LiveGraph> LoadAsync(CancellationToken cancellationToken)
    {
        var live = (await content.ListLiveAsync(cancellationToken).ConfigureAwait(false)).Where(item => item.HasPage).ToList();
        var snapshots = await revisions.GetManyAsync(live.Select(item => item.LiveRevisionId!.Value).ToList(), cancellationToken).ConfigureAwait(false);
        var currentPaths = await paths.GetCurrentPathsAsync(live.Select(item => item.Id).ToList(), cancellationToken).ConfigureAwait(false);

        var pages = new List<PageInfo>();
        var linkedIds = new HashSet<Guid>();
        foreach (var item in live)
        {
            if (!snapshots.TryGetValue(item.LiveRevisionId!.Value, out var revision))
            {
                continue;
            }

            foreach (var translation in ContentSnapshotSerializer.Deserialize(revision.Snapshot).Translations)
            {
                var spans = translation.Body.SelectMany(block => (block.Spans ?? []).Concat((block.Items ?? []).SelectMany(listItem => listItem.Spans))).ToList();
                var links = spans.Where(span => span.Link is not null).Select(span => (span.Text, span.Link!)).ToList();
                linkedIds.UnionWith(links.Select(link => link.Item2.ContentItemId).OfType<Guid>());
                var text = string.Join(' ', spans.Where(span => span.Link is null).Select(span => span.Text));
                pages.Add(new PageInfo(
                    item.Id,
                    translation.LanguageCode,
                    translation.Title,
                    currentPaths.TryGetValue((item.Id, translation.LanguageCode), out var path) ? $"/{translation.LanguageCode}/{path}" : string.Empty,
                    links,
                    $" {Normalize(text)} ",
                    Normalize(translation.Seo.PrimaryKeyword ?? string.Empty),
                    translation.Seo.PrimaryKeyword ?? string.Empty));
            }
        }

        var known = (await content.GetLookupAsync(linkedIds.ToList(), cancellationToken).ConfigureAwait(false)).Keys.ToHashSet();
        return new LiveGraph(pages, live.Select(item => item.Id).ToHashSet(), known);
    }

    /// <summary>Keyword comparison form with punctuation treated as word separators.</summary>
    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in TextNormalization.NormalizeKeyword(value))
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Short(string value) => value.Length > 60 ? value[..60] + "…" : value;

    private sealed record PageInfo(
        Guid Id, string Language, string Title, string Path, List<(string Anchor, LinkTarget Link)> Links, string NormalizedText, string Keyword, string RawKeyword);

    private sealed record LiveGraph(List<PageInfo> Pages, HashSet<Guid> LiveIds, HashSet<Guid> KnownIds);
}
