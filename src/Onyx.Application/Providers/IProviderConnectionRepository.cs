using Onyx.Domain.Providers;

namespace Onyx.Application.Providers;

public interface IProviderConnectionRepository
{
    Task<ProviderConnection?> GetByKeyAsync(string providerKey, CancellationToken cancellationToken);

    void Add(ProviderConnection connection);
}
