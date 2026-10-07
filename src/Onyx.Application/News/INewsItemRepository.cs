using Onyx.Contracts.Common;
using Onyx.Domain.News;

namespace Onyx.Application.News;

public interface INewsItemRepository
{
    /// <summary>The item with its translations (tracked).</summary>
    Task<NewsItem?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Which of these canonical-URL hashes already exist (any status, deleted tombstones included).</summary>
    Task<IReadOnlySet<string>> ExistingUrlHashesAsync(IReadOnlyCollection<string> hashes, CancellationToken cancellationToken);

    /// <summary>The most recent item in this language with the same cluster key ingested since <paramref name="since"/>.</summary>
    Task<Guid?> FindRecentInClusterAsync(string clusterKey, string languageCode, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>An item (other than <paramref name="exceptId"/>) already uses this slug in this language.</summary>
    Task<bool> SlugTakenAsync(string languageCode, string slug, Guid? exceptId, CancellationToken cancellationToken);

    Task<PagedResult<NewsItem>> ListAsync(
        NewsItemStatus? status, Guid? sourceId, Guid? categoryId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Published items with a translation in <paramref name="languageCode"/>, newest first (no tracking).</summary>
    Task<PagedResult<NewsItem>> ListPublishedAsync(string languageCode, Guid? categoryId, int page, int pageSize, CancellationToken cancellationToken);

    Task<NewsItem?> GetPublishedBySlugAsync(string languageCode, string slug, CancellationToken cancellationToken);

    /// <summary>Published, indexable items (sitemap and search reconcile), newest first, at most <paramref name="max"/>.</summary>
    Task<IReadOnlyList<NewsItem>> ListPublishedIndexableAsync(int max, CancellationToken cancellationToken);

    /// <summary>Categories that have at least one published item in <paramref name="languageCode"/>, with the latest publication time.</summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> LatestPublishedByCategoryAsync(string languageCode, CancellationToken cancellationToken);

    void Add(NewsItem item);

    void SetExpectedVersion(NewsItem item, uint version);
}
