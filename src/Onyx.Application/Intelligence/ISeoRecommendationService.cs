using Onyx.Contracts.Common;
using Onyx.Contracts.Intelligence;
using Onyx.Domain.Common;

namespace Onyx.Application.Intelligence;

public interface ISeoRecommendationService
{
    Task<Result<PagedResult<RecommendationResponse>>> ListAsync(string? status, Guid? contentItemId, string? kind, int page, int pageSize, CancellationToken cancellationToken);

    Task<Result<RecommendationResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<RecommendationResponse>> ProposeAsync(ProposeRecommendationRequest request, CancellationToken cancellationToken);

    Task<Result<RecommendationResponse>> ApproveAsync(Guid id, DecideRequest request, CancellationToken cancellationToken);

    Task<Result<RecommendationResponse>> RejectAsync(Guid id, DecideRequest request, CancellationToken cancellationToken);

    /// <summary>Applies an approved field change to the working copy (Draft); publishing still requires review.</summary>
    Task<Result<RecommendationResponse>> ApplyAsync(Guid id, DecideRequest request, CancellationToken cancellationToken);

    /// <summary>Records that an editor applied the change by hand (for changes that are not single fields).</summary>
    Task<Result<RecommendationResponse>> MarkAppliedAsync(Guid id, DecideRequest request, CancellationToken cancellationToken);
}
