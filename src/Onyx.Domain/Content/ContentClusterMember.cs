using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

public sealed class ContentClusterMember : Entity
{
    private ContentClusterMember()
    {
    }

    public Guid ClusterId { get; private set; }

    public Guid ContentItemId { get; private set; }

    public ClusterRole Role { get; private set; }

    internal static ContentClusterMember Create(Guid clusterId, Guid contentItemId, ClusterRole role) =>
        new() { ClusterId = clusterId, ContentItemId = contentItemId, Role = role };

    internal void ChangeRole(ClusterRole role) => Role = role;
}
