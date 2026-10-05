using Onyx.Application.Localization;
using Onyx.Contracts.Content;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>
/// Resolves a public URL to live content, a single permanent redirect to the final current URL (never a chain), 410 Gone or 404.
/// Loops are detected and answered 404 rather than followed.
/// </summary>
public sealed class PublicContentService(
    IContentRepository content,
    IContentPathRepository paths,
    IContentRevisionRepository revisions,
    IRedirectRepository redirects,
    IContentRelationRepository relations,
    Media.IMediaRepository media,
    Proof.IFeaturedProjectRepository featured,
    PublicContentBuilder builder) : IPublicContentService
{
    public const int ListingPageSize = 20;

    /// <summary>API section name → the content type it lists.</summary>
    public static IReadOnlyDictionary<string, ContentType> SectionTypes { get; } = new Dictionary<string, ContentType>(StringComparer.Ordinal)
    {
        ["work"] = ContentType.Project,
        ["case-studies"] = ContentType.CaseStudy,
        ["insights"] = ContentType.Insight,
        ["topics"] = ContentType.Topic,
        ["glossary"] = ContentType.GlossaryTerm,
        ["integrations"] = ContentType.Integration,
    };

    public const string KindContent = "Content";
    public const string KindRedirect = "Redirect";
    public const string KindGone = "Gone";
    public const string KindNotFound = "NotFound";

    private const int MaxHops = 6;

    private static readonly ContentResolveResponse NotFound = new(KindNotFound, null, null);
    private static readonly ContentResolveResponse Gone = new(KindGone, null, null);

    public async Task<ContentResolveResponse> ResolveAsync(string languageCode, string path, CancellationToken cancellationToken)
    {
        if (!SupportedLanguages.All.Contains(languageCode))
        {
            return NotFound;
        }

        var requested = ContentPathService.NormalizePath(path);
        if (requested.Length is 0 or > ContentPath.PathMaxLength)
        {
            return NotFound;
        }

        var visitedPaths = new HashSet<string>(StringComparer.Ordinal);
        var current = requested;
        for (var hop = 0; hop < MaxHops; hop++)
        {
            if (!visitedPaths.Add(current))
            {
                return NotFound;
            }

            var owned = await paths.FindAsync(languageCode, current, cancellationToken).ConfigureAwait(false);
            if (owned is not null)
            {
                var items = await content.GetLookupAsync([owned.ContentItemId], cancellationToken).ConfigureAwait(false);
                if (!items.TryGetValue(owned.ContentItemId, out var item))
                {
                    return NotFound;
                }

                if (item.IsLive && owned.IsCurrent && hop == 0)
                {
                    return await ServeAsync(item, languageCode, current, cancellationToken).ConfigureAwait(false);
                }

                return await FollowItemAsync(item, languageCode, requested, cancellationToken).ConfigureAwait(false);
            }

            var rule = await redirects.FindActiveAsync(languageCode, current, cancellationToken).ConfigureAwait(false);
            if (rule is null)
            {
                return NotFound;
            }

            if (rule.Kind == RedirectKind.Gone)
            {
                return Gone;
            }

            if (rule.TargetContentItemId is { } targetId)
            {
                var targets = await content.GetLookupAsync([targetId], cancellationToken).ConfigureAwait(false);
                return targets.TryGetValue(targetId, out var target)
                    ? await FollowItemAsync(target, languageCode, requested, cancellationToken).ConfigureAwait(false)
                    : NotFound;
            }

            var next = ContentPathService.NormalizePath(rule.TargetPath);
            var nextOwner = await paths.FindAsync(languageCode, next, cancellationToken).ConfigureAwait(false);
            if (nextOwner is { IsCurrent: true } && (await content.GetLookupAsync([nextOwner.ContentItemId], cancellationToken).ConfigureAwait(false))
                    .GetValueOrDefault(nextOwner.ContentItemId) is { IsLive: true })
            {
                return Redirect(languageCode, next, requested);
            }

            current = next;
        }

        return NotFound;
    }

    public async Task<IReadOnlyList<SitemapEntryResponse>> GetSitemapAsync(string languageCode, CancellationToken cancellationToken)
    {
        if (!SupportedLanguages.All.Contains(languageCode))
        {
            return [];
        }

        var live = (await content.ListLiveAsync(cancellationToken).ConfigureAwait(false)).Where(item => item.HasPage).ToList();
        if (live.Count == 0)
        {
            return [];
        }

        var snapshots = await revisions.GetManyAsync(live.Select(item => item.LiveRevisionId!.Value).ToList(), cancellationToken).ConfigureAwait(false);
        var currentPaths = await paths.GetCurrentPathsAsync(live.Select(item => item.Id).ToList(), cancellationToken).ConfigureAwait(false);
        var entries = new List<SitemapEntryResponse>();

        foreach (var item in live)
        {
            if (!snapshots.TryGetValue(item.LiveRevisionId!.Value, out var revision))
            {
                continue;
            }

            var snapshot = ContentSnapshotSerializer.Deserialize(revision.Snapshot);
            bool Listed(string language) => PublicContentBuilder.IsHreflangEligible(snapshot, language) && currentPaths.ContainsKey((item.Id, language));

            if (!Listed(languageCode))
            {
                continue;
            }

            var alternates = SupportedLanguages.All.Where(Listed)
                .ToDictionary(language => language, language => $"/{language}/{currentPaths[(item.Id, language)]}", StringComparer.Ordinal);
            entries.Add(new SitemapEntryResponse(
                alternates[languageCode], item.GetTranslation(languageCode)?.LiveContentModifiedAtUtc ?? item.LastPublishedAtUtc, alternates));
        }

        return entries.OrderBy(entry => entry.Path, StringComparer.Ordinal).ToList();
    }

    private async Task<ContentResolveResponse> ServeAsync(ContentItem item, string languageCode, string path, CancellationToken cancellationToken)
    {
        var revision = await revisions.GetAsync(item.LiveRevisionId!.Value, cancellationToken).ConfigureAwait(false);
        if (revision is null)
        {
            return NotFound;
        }

        var snapshot = ContentSnapshotSerializer.Deserialize(revision.Snapshot);
        if (snapshot.Translation(languageCode) is null)
        {
            return NotFound;
        }

        return new ContentResolveResponse(KindContent, null, await builder.BuildAsync(item, snapshot, languageCode, $"/{languageCode}/{path}", cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Follows an item (and editor-chosen replacements) to the final live URL in one step.</summary>
    private async Task<ContentResolveResponse> FollowItemAsync(ContentItem start, string languageCode, string requested, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        var item = start;
        for (var hop = 0; hop < MaxHops; hop++)
        {
            if (!visited.Add(item.Id))
            {
                return NotFound;
            }

            if (item.IsLive)
            {
                var current = await paths.GetCurrentPathsAsync([item.Id], cancellationToken).ConfigureAwait(false);
                return current.TryGetValue((item.Id, languageCode), out var path) ? Redirect(languageCode, path, requested) : NotFound;
            }

            if (item.OfflineResolution != OfflineResolution.RedirectToContent || item.OfflineRedirectToId is not { } next)
            {
                return Gone;
            }

            var targets = await content.GetLookupAsync([next], cancellationToken).ConfigureAwait(false);
            if (!targets.TryGetValue(next, out var target))
            {
                return Gone;
            }

            item = target;
        }

        return NotFound;
    }

    public async Task<Domain.Common.Result<Contracts.Common.PagedResult<ListingItemResponse>>> ListAsync(
        string languageCode, string section, Guid? topicId, int page, CancellationToken cancellationToken)
    {
        if (!SupportedLanguages.All.Contains(languageCode) || !SectionTypes.TryGetValue(section ?? string.Empty, out var type))
        {
            return Domain.Common.Error.NotFound("Listing.NotFound", "Unknown section.");
        }

        var live = (await content.ListLiveAsync(cancellationToken).ConfigureAwait(false)).Where(item => item.Type == type && item.HasPage).ToList();
        if (topicId is { } topic)
        {
            if (type != ContentType.Insight)
            {
                return Domain.Common.Error.Validation("Listing.TopicFilter", "Only insights can be listed by topic.");
            }

            var tagged = (await relations.ListToAsync(topic, cancellationToken).ConfigureAwait(false)).Select(relation => relation.FromContentItemId).ToHashSet();
            live = live.Where(item => tagged.Contains(item.Id)).ToList();
        }

        var entries = await ListingEntriesAsync(live, languageCode, cancellationToken).ConfigureAwait(false);
        var ordered = type == ContentType.Insight
            ? entries.OrderByDescending(entry => entry.Item.FirstPublishedAtUtc).ThenBy(entry => entry.Response.Title, StringComparer.Ordinal)
            : entries.OrderBy(entry => entry.Item.SortOrder).ThenBy(entry => entry.Response.Title, StringComparer.Ordinal);
        var pageNumber = Math.Max(page, 1);
        var items = ordered.Skip((pageNumber - 1) * ListingPageSize).Take(ListingPageSize).Select(entry => entry.Response).ToList();
        return new Contracts.Common.PagedResult<ListingItemResponse>(items, pageNumber, ListingPageSize, entries.Count);
    }

    public async Task<IReadOnlyList<ListingItemResponse>> GetFeaturedProjectsAsync(string languageCode, CancellationToken cancellationToken)
    {
        if (!SupportedLanguages.All.Contains(languageCode))
        {
            return [];
        }

        var slots = (await featured.ListAsync(cancellationToken).ConfigureAwait(false)).OrderBy(slot => slot.Slot).ToList();
        var items = await content.GetLookupAsync(slots.Select(slot => slot.ContentItemId).ToList(), cancellationToken).ConfigureAwait(false);
        var live = slots
            .Select(slot => items.GetValueOrDefault(slot.ContentItemId))
            .Where(item => item is { IsLive: true, Type: ContentType.Project, HasPage: true })
            .Select(item => item!)
            .ToList();
        var entries = await ListingEntriesAsync(live, languageCode, cancellationToken).ConfigureAwait(false);
        return live.Select(item => entries.FirstOrDefault(entry => entry.Item.Id == item.Id)).OfType<(ContentItem Item, ListingItemResponse Response)>()
            .Select(entry => entry.Response).ToList();
    }

    /// <summary>Titles and summaries come from each item's live revision, never from unreviewed working copies.</summary>
    private async Task<List<(ContentItem Item, ListingItemResponse Response)>> ListingEntriesAsync(
        IReadOnlyList<ContentItem> live, string languageCode, CancellationToken cancellationToken)
    {
        if (live.Count == 0)
        {
            return [];
        }

        var snapshots = await revisions.GetManyAsync(live.Select(item => item.LiveRevisionId!.Value).ToList(), cancellationToken).ConfigureAwait(false);
        var currentPaths = await paths.GetCurrentPathsAsync(live.Select(item => item.Id).ToList(), cancellationToken).ConfigureAwait(false);
        var parsed = live
            .Where(item => snapshots.ContainsKey(item.LiveRevisionId!.Value))
            .Select(item => (Item: item, Snapshot: ContentSnapshotSerializer.Deserialize(snapshots[item.LiveRevisionId!.Value].Snapshot)))
            .ToList();
        var images = await media.GetLookupAsync(parsed.Select(entry => entry.Snapshot.FeaturedImageId).OfType<Guid>().Distinct().ToList(), cancellationToken).ConfigureAwait(false);

        var result = new List<(ContentItem, ListingItemResponse)>();
        foreach (var (item, snapshot) in parsed)
        {
            if (snapshot.Translation(languageCode) is not { } translation || !currentPaths.TryGetValue((item.Id, languageCode), out var path))
            {
                continue;
            }

            var image = snapshot.FeaturedImageId is { } imageId && images.TryGetValue(imageId, out var asset)
                ? PublicContentBuilder.ToMedia(asset, null, null, languageCode)
                : null;
            result.Add((item, new ListingItemResponse(
                item.Id, item.Type.ToString(), translation.Title, string.IsNullOrWhiteSpace(translation.Summary) ? null : translation.Summary,
                $"/{languageCode}/{path}", item.FirstPublishedAtUtc, image)));
        }

        return result;
    }

    private static ContentResolveResponse Redirect(string languageCode, string path, string requested) =>
        string.Equals(path, requested, StringComparison.Ordinal) ? NotFound : new ContentResolveResponse(KindRedirect, $"/{languageCode}/{path}", null);
}
