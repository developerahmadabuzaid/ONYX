using Onyx.Contracts.Content;
using Onyx.Domain.Common;

namespace Onyx.Application.Content;

public interface IContentClusterService
{
    Task<IReadOnlyList<ClusterResponse>> ListAsync(CancellationToken cancellationToken);

    Task<Result<ClusterResponse>> CreateAsync(SaveClusterRequest request, CancellationToken cancellationToken);

    Task<Result<ClusterResponse>> UpdateAsync(Guid id, SaveClusterRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<ClusterResponse>> SetMemberAsync(Guid id, SetClusterMemberRequest request, CancellationToken cancellationToken);

    Task<Result<ClusterResponse>> RemoveMemberAsync(Guid id, Guid contentItemId, CancellationToken cancellationToken);
}
