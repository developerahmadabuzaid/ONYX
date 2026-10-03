using Microsoft.EntityFrameworkCore;
using Onyx.Application.Providers;
using Onyx.Domain.Providers;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class ProviderConnectionRepository(ApplicationDbContext dbContext) : IProviderConnectionRepository
{
    public Task<ProviderConnection?> GetByKeyAsync(string providerKey, CancellationToken cancellationToken) =>
        dbContext.ProviderConnections.SingleOrDefaultAsync(connection => connection.ProviderKey == providerKey, cancellationToken);

    public void Add(ProviderConnection connection) => dbContext.ProviderConnections.Add(connection);

    public async Task<IReadOnlyList<ProviderConnection>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.ProviderConnections.AsNoTracking().OrderBy(connection => connection.ProviderKey).ToListAsync(cancellationToken).ConfigureAwait(false);
}
