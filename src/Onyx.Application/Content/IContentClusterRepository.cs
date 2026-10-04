using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public interface IContentClusterRepository
{
    Task<ContentCluster?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContentCluster>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Read-only clusters that contain the item.</summary>
    Task<IReadOnlyList<ContentCluster>> ListContainingAsync(Guid contentItemId, CancellationToken cancellationToken);

    void Add(ContentCluster cluster);

    void Remove(ContentCluster cluster);

    void SetExpectedVersion(ContentCluster cluster, uint version);
}
