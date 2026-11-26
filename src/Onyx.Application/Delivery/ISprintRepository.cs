using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

public interface ISprintRepository
{
    Task<Sprint?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Guid?> ActiveSprintIdAsync(Guid projectId, CancellationToken cancellationToken);

    void Add(Sprint sprint);

    void SetExpectedVersion(Sprint sprint, uint version);
}
