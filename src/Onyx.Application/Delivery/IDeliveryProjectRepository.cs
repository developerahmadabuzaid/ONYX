using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

public interface IDeliveryProjectRepository
{
    /// <summary>The project with its workflow and members, tracked for changes.</summary>
    Task<DeliveryProject?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken);

    void Add(DeliveryProject project);

    void SetExpectedVersion(DeliveryProject project, uint version);
}
