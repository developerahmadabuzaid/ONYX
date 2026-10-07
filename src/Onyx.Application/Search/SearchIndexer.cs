using System.Text;
using Onyx.Application.Content;
using Onyx.Application.Localization;
using Onyx.Domain.Content;

namespace Onyx.Application.Search;

/// <summary>
/// Keeps the search index equal to what the public site serves (architecture.md §18). A page is indexed in a language only when
/// it is live, has a page, its live snapshot has that language, it has a current URL, and that language is indexable (no
/// noindex, no canonical pointing elsewhere), the same rule as the sitemap. Everything else is removed. Only public text is
/// indexed (title, summary, body, FAQs, slug); internal SEO planning such as target keywords and notes never is. Proof-bearing
/// pages can only be live once verified, so the index cannot expose unverified proof. Idempotent.
/// </summary>
public sealed class SearchIndexer(
    IContentRepository content,
    IContentRevisionRepository revisions,
    IContentPathRepository paths,
    ISearchDocumentRepository index)
{
    /// <summary>Reconcile cap: pages re-indexed per run (the rest follows on the next run).</summary>
    public const int MaxReindexPerRun = 500;

    /// <summary>Indexed body text per page and language (characters); keeps every document well inside the tsvector limit.</summary>
    public const int MaxBodyCharacters = 100_000;

    public async Task<int> ReindexAsync(Guid contentItemId, CancellationToken cancellationToken)
    {
        var item = await content.GetAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        if (item is not { IsLive: true, HasPage: true })
        {
            return await index.RemoveAsync(contentItemId, null, cancellationToken).ConfigureAwait(false);
        }

        var revision = await revisions.GetAsync(item.LiveRevisionId!.Value, cancellationToken).ConfigureAwait(false);
        if (revision is null)
        {
            return await index.RemoveAsync(contentItemId, null, cancellationToken).ConfigureAwait(false);
        }

        var snapshot = ContentSnapshotSerializer.Deserialize(revision.Snapshot);
        var currentPaths = await paths.GetCurrentPathsAsync([item.Id], cancellationToken).ConfigureAwait(false);
        var changed = 0;
        foreach (var language in SupportedLanguages.All.Order(StringComparer.Ordinal))
        {
            var translation = snapshot.Translation(language);
            if (translation is null || !PublicContentBuilder.IsHreflangEligible(snapshot, language) || !currentPaths.TryGetValue((item.Id, language), out var path))
            {
                changed += await index.RemoveAsync(item.Id, language, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await index.UpsertAsync(Build(item, snapshot.Type, translation, $"/{language}/{path}", item.LiveRevisionId.Value), cancellationToken).ConfigureAwait(false);
            changed++;
        }

        return changed;
    }

    /// <summary>
    /// Heals anything an event missed (and fills the index the first time): re-indexes live pages that are missing, stale (newer
    /// live revision) or moved, and removes documents of pages that are no longer live. Bounded per run.
    /// </summary>
    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        var live = (await content.ListLiveAsync(cancellationToken).ConfigureAwait(false)).Where(item => item.HasPage).ToList();
        var liveById = live.ToDictionary(item => item.Id);
        // News documents belong to NewsSearchIndexer; this reconcile only owns CMS pages.
        var states = (await index.ListStatesAsync(cancellationToken).ConfigureAwait(false)).Where(state => state.Group != SearchGroups.News).ToList();
        var currentPaths = await paths.GetCurrentPathsAsync(live.Select(item => item.Id).ToList(), cancellationToken).ConfigureAwait(false);

        var due = new HashSet<Guid>(states.Where(state => !liveById.ContainsKey(state.ContentItemId)).Select(state => state.ContentItemId));
        var indexed = states.ToDictionary(state => (state.ContentItemId, state.LanguageCode));
        foreach (var item in live)
        {
            foreach (var language in SupportedLanguages.All)
            {
                var hasPath = currentPaths.TryGetValue((item.Id, language), out var path);
                var known = indexed.TryGetValue((item.Id, language), out var state);
                if ((hasPath && !known) || (known && (state!.LiveRevisionId != item.LiveRevisionId || !hasPath || state.Path != $"/{language}/{path}")))
                {
                    due.Add(item.Id);
                }
            }
        }

        var processed = 0;
        foreach (var contentItemId in due.Order().Take(MaxReindexPerRun))
        {
            await ReindexAsync(contentItemId, cancellationToken).ConfigureAwait(false);
            processed++;
        }

        return processed;
    }

    private static SearchDocumentData Build(ContentItem item, ContentType type, TranslationSnapshot translation, string path, Guid revisionId)
    {
        var body = new StringBuilder();
        foreach (var block in translation.Body)
        {
            Append(body, block.Spans);
            foreach (var listItem in block.Items ?? [])
            {
                Append(body, listItem.Spans);
            }

            if (block.Caption is { Length: > 0 } caption)
            {
                body.Append(caption).Append(' ');
            }
        }

        foreach (var faq in translation.Faqs)
        {
            body.Append(faq.Question).Append(' ').Append(faq.Answer).Append(' ');
        }

        var group = SearchGroups.For(type);
        return new SearchDocumentData(
            item.Id,
            translation.LanguageCode,
            type.ToString(),
            group,
            translation.Title,
            translation.Summary,
            path,
            item.FirstPublishedAtUtc,
            revisionId,
            SearchTextNormalizer.ForIndex(translation.Title),
            SearchTextNormalizer.ForIndex(translation.Summary),
            SearchTextNormalizer.ForIndex(body.Length > MaxBodyCharacters ? body.ToString(0, MaxBodyCharacters) : body.ToString()),
            SearchTextNormalizer.Normalize($"{translation.Title} {translation.Slug.Replace('-', ' ')}"));
    }

    private static void Append(StringBuilder body, IReadOnlyList<TextSpan>? spans)
    {
        foreach (var span in spans ?? [])
        {
            body.Append(span.Text);
        }

        body.Append(' ');
    }
}
