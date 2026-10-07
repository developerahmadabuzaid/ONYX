using Onyx.Domain.Analytics;

namespace Onyx.Application.Analytics;

public interface IAnalyticsSyncStateRepository
{
    /// <summary>The state row for the provider (tracked), or null before its first sync.</summary>
    Task<AnalyticsSyncState?> GetAsync(string providerKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<AnalyticsSyncState>> ListAsync(CancellationToken cancellationToken);

    void Add(AnalyticsSyncState state);
}
