using Onyx.Contracts.Content;
using Onyx.Domain.Common;

namespace Onyx.Application.Content;

public interface IContentRevisionService
{
    Task<Result<IReadOnlyList<ContentRevisionSummaryResponse>>> ListAsync(Guid contentItemId, CancellationToken cancellationToken);

    Task<Result<ContentRevisionResponse>> GetAsync(Guid contentItemId, int number, CancellationToken cancellationToken);

    Task<Result<RevisionComparisonResponse>> CompareAsync(Guid contentItemId, int fromNumber, int toNumber, CancellationToken cancellationToken);

    /// <summary>Copies an old revision into the working copy as a new revision; history is never rewritten.</summary>
    Task<Result<ContentDetailResponse>> RestoreAsync(Guid contentItemId, int number, RestoreRevisionRequest request, CancellationToken cancellationToken);
}
