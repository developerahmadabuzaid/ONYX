using Onyx.Contracts.Administration;
using Onyx.Domain.Common;

namespace Onyx.Application.Providers;

public interface IProviderConnectionAdministrationService
{
    Task<IReadOnlyList<ProviderConnectionResponse>> ListAsync(CancellationToken cancellationToken);

    Task<Result<ProviderConnectionResponse>> DisableAsync(string providerKey, CancellationToken cancellationToken);

    Task<Result<ProviderConnectionResponse>> EnableAsync(string providerKey, CancellationToken cancellationToken);

    /// <summary>Queues the provider test email to the current admin's own address. Delivery (and the provider status) is handled by the Worker.</summary>
    Task<Result<Guid>> QueueEmailTestAsync(CancellationToken cancellationToken);
}
