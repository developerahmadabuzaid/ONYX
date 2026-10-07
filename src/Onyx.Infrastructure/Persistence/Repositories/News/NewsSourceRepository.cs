using Microsoft.EntityFrameworkCore;
using Onyx.Application.News;
using Onyx.Domain.News;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class NewsSourceRepository(ApplicationDbContext dbContext) : INewsSourceRepository
{
    public Task<NewsSource?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.NewsSources.SingleOrDefaultAsync(source => source.Id == id, cancellationToken);

    public async Task<IReadOnlyList<NewsSource>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.NewsSources.AsNoTracking().OrderBy(source => source.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> ListDueIdsAsync(DateTimeOffset now, int max, CancellationToken cancellationToken) =>
        await dbContext.NewsSources.AsNoTracking()
            .Where(source => source.Status == NewsSourceStatus.Allowed && (source.NextFetchAtUtc == null || source.NextFetchAtUtc <= now))
            .OrderBy(source => source.NextFetchAtUtc)
            .ThenBy(source => source.Id)
            .Select(source => source.Id)
            .Take(max)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> FeedUrlExistsAsync(string feedUrl, Guid? exceptId, CancellationToken cancellationToken) =>
        dbContext.NewsSources.AnyAsync(source => source.FeedUrl == feedUrl && (exceptId == null || source.Id != exceptId), cancellationToken);

    public void Add(NewsSource source) => dbContext.NewsSources.Add(source);

    public void SetExpectedVersion(NewsSource source, uint version) =>
        dbContext.Entry(source).Property(entry => entry.Version).OriginalValue = version;
}
