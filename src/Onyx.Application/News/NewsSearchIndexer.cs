using Onyx.Application.Search;
using Onyx.Domain.News;

namespace Onyx.Application.News;

/// <summary>
/// Keeps news in the site search index (architecture.md §18, group News): an item is indexed in a language only while it is
/// <see cref="NewsItemStatus.Published"/> and marked indexable (the same rule as the sitemap; held, hidden, unpublished,
/// deleted and noindex items are removed). Only Onyx's own headline and summary and the publisher name are indexed.
/// Idempotent; driven by <see cref="NewsItemPublicationChanged"/> and healed by the bounded reconcile.
/// </summary>
public sealed class NewsSearchIndexer(INewsItemRepository items, ISearchDocumentRepository index)
{
    public const int MaxReindexPerRun = 500;

    public async Task<int> ReindexAsync(Guid newsItemId, CancellationToken cancellationToken)
    {
        var item = await items.GetAsync(newsItemId, cancellationToken).ConfigureAwait(false);
        if (item is not { IsPublic: true, Indexable: true })
        {
            return await index.RemoveAsync(newsItemId, null, cancellationToken).ConfigureAwait(false);
        }

        foreach (var translation in item.Translations)
        {
            await index.UpsertAsync(Build(item, translation), cancellationToken).ConfigureAwait(false);
        }

        return item.Translations.Count;
    }

    /// <summary>Indexes eligible items that are missing or moved and removes news documents of items that are no longer eligible.</summary>
    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        var eligible = await items.ListPublishedIndexableAsync(PublicNewsService.MaxSitemapItems, cancellationToken).ConfigureAwait(false);
        var states = (await index.ListStatesAsync(cancellationToken).ConfigureAwait(false)).Where(state => state.Group == SearchGroups.News).ToList();
        var indexed = states.ToDictionary(state => (state.ContentItemId, state.LanguageCode));
        var eligibleIds = eligible.Select(item => item.Id).ToHashSet();

        var due = new HashSet<Guid>(states.Where(state => !eligibleIds.Contains(state.ContentItemId)).Select(state => state.ContentItemId));
        foreach (var item in eligible)
        {
            foreach (var translation in item.Translations)
            {
                if (!indexed.TryGetValue((item.Id, translation.LanguageCode), out var state)
                    || state.Path != PublicNewsService.ItemPath(translation.LanguageCode, translation.Slug))
                {
                    due.Add(item.Id);
                }
            }
        }

        var processed = 0;
        foreach (var id in due.Order().Take(MaxReindexPerRun))
        {
            await ReindexAsync(id, cancellationToken).ConfigureAwait(false);
            processed++;
        }

        return processed;
    }

    private static SearchDocumentData Build(NewsItem item, NewsItemTranslation translation) =>
        new(
            item.Id,
            translation.LanguageCode,
            SearchGroups.NewsItemType,
            SearchGroups.News,
            translation.Title,
            translation.Summary,
            PublicNewsService.ItemPath(translation.LanguageCode, translation.Slug),
            item.PublishedAtUtc,
            item.Id,
            SearchTextNormalizer.ForIndex(translation.Title),
            SearchTextNormalizer.ForIndex(translation.Summary),
            string.Empty,
            SearchTextNormalizer.Normalize($"{translation.Title} {item.Publisher}"));
}
