using Onyx.Domain.News;

namespace Onyx.Application.News;

public interface INewsSourceRepository
{
    Task<NewsSource?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>All sources (administration; the allowlist is small by nature).</summary>
    Task<IReadOnlyList<NewsSource>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Allowed sources due for a fetch, the longest-waiting first, at most <paramref name="max"/>.</summary>
    Task<IReadOnlyList<Guid>> ListDueIdsAsync(DateTimeOffset now, int max, CancellationToken cancellationToken);

    Task<bool> FeedUrlExistsAsync(string feedUrl, Guid? exceptId, CancellationToken cancellationToken);

    void Add(NewsSource source);

    void SetExpectedVersion(NewsSource source, uint version);
}
