using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

public interface ISavedIssueFilterRepository
{
    Task<SavedIssueFilter?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<int> CountForOwnerAsync(Guid ownerId, CancellationToken cancellationToken);

    void Add(SavedIssueFilter filter);

    void Remove(SavedIssueFilter filter);
}
