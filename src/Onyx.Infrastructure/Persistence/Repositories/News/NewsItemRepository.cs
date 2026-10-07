using Microsoft.EntityFrameworkCore;
using Onyx.Application.News;
using Onyx.Contracts.Common;
using Onyx.Domain.News;

namespace Onyx.Infrastructure.Persistence.Repositories;

/// <summary>News items. Public queries filter on <see cref="NewsItemStatus.Published"/> in the database, never in memory.</summary>
internal sealed class NewsItemRepository(ApplicationDbContext dbContext) : INewsItemRepository
{
    public Task<NewsItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.NewsItems.Include(item => item.Translations).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlySet<string>> ExistingUrlHashesAsync(IReadOnlyCollection<string> hashes, CancellationToken cancellationToken)
    {
        if (hashes.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var found = await dbContext.NewsItems.AsNoTracking()
            .Where(item => hashes.Contains(item.OriginalUrlHash))
            .Select(item => item.OriginalUrlHash)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return found.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<Guid?> FindRecentInClusterAsync(string clusterKey, string languageCode, DateTimeOffset since, CancellationToken cancellationToken) =>
        await dbContext.NewsItems.AsNoTracking()
            .Where(item => item.ClusterKey == clusterKey && item.SourceLanguageCode == languageCode && item.IngestedAtUtc >= since)
            .OrderByDescending(item => item.IngestedAtUtc)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> SlugTakenAsync(string languageCode, string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        dbContext.NewsItemTranslations.AnyAsync(
            translation => translation.LanguageCode == languageCode && translation.Slug == slug && (exceptId == null || translation.NewsItemId != exceptId),
            cancellationToken);

    public async Task<PagedResult<NewsItem>> ListAsync(
        NewsItemStatus? status, Guid? sourceId, Guid? categoryId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.NewsItems.AsNoTracking();
        if (status is { } wanted)
        {
            query = query.Where(item => item.Status == wanted);
        }

        if (sourceId is { } source)
        {
            query = query.Where(item => item.SourceId == source);
        }

        if (categoryId is { } category)
        {
            query = query.Where(item => item.CategoryId == category);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.Include(item => item.Translations)
            .OrderByDescending(item => item.IngestedAtUtc).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedResult<NewsItem>(items, page, pageSize, total);
    }

    public async Task<PagedResult<NewsItem>> ListPublishedAsync(string languageCode, Guid? categoryId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.NewsItems.AsNoTracking()
            .Where(item => item.Status == NewsItemStatus.Published && item.Translations.Any(translation => translation.LanguageCode == languageCode));
        if (categoryId is { } category)
        {
            query = query.Where(item => item.CategoryId == category);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.Include(item => item.Translations)
            .OrderByDescending(item => item.PublishedAtUtc).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedResult<NewsItem>(items, page, pageSize, total);
    }

    public Task<NewsItem?> GetPublishedBySlugAsync(string languageCode, string slug, CancellationToken cancellationToken) =>
        dbContext.NewsItems.AsNoTracking()
            .Include(item => item.Translations)
            .Where(item => item.Status == NewsItemStatus.Published
                && item.Translations.Any(translation => translation.LanguageCode == languageCode && translation.Slug == slug))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<NewsItem>> ListPublishedIndexableAsync(int max, CancellationToken cancellationToken) =>
        await dbContext.NewsItems.AsNoTracking()
            .Include(item => item.Translations)
            .Where(item => item.Status == NewsItemStatus.Published && item.Indexable)
            .OrderByDescending(item => item.PublishedAtUtc).ThenByDescending(item => item.Id)
            .Take(max)
            .AsSplitQuery()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> LatestPublishedByCategoryAsync(string languageCode, CancellationToken cancellationToken)
    {
        var rows = await dbContext.NewsItems.AsNoTracking()
            .Where(item => item.Status == NewsItemStatus.Published && item.PublishedAtUtc != null
                && item.Translations.Any(translation => translation.LanguageCode == languageCode))
            .GroupBy(item => item.CategoryId)
            .Select(group => new { CategoryId = group.Key, Latest = group.Max(item => item.PublishedAtUtc!.Value) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.ToDictionary(row => row.CategoryId, row => row.Latest);
    }

    public void Add(NewsItem item) => dbContext.NewsItems.Add(item);

    public void SetExpectedVersion(NewsItem item, uint version) =>
        dbContext.Entry(item).Property(entry => entry.Version).OriginalValue = version;
}
