using Microsoft.EntityFrameworkCore;
using Onyx.Application.Analytics;
using Onyx.Domain.Analytics;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class AnalyticsSyncStateRepository(ApplicationDbContext dbContext) : IAnalyticsSyncStateRepository
{
    public Task<AnalyticsSyncState?> GetAsync(string providerKey, CancellationToken cancellationToken) =>
        dbContext.AnalyticsSyncStates.SingleOrDefaultAsync(state => state.ProviderKey == providerKey, cancellationToken);

    public async Task<IReadOnlyList<AnalyticsSyncState>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.AnalyticsSyncStates.AsNoTracking().OrderBy(state => state.ProviderKey).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(AnalyticsSyncState state) => dbContext.AnalyticsSyncStates.Add(state);
}
