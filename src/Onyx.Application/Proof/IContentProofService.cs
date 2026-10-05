using Onyx.Contracts.Content;
using Onyx.Domain.Common;

namespace Onyx.Application.Proof;

/// <summary>Proof verification of projects, case studies and integrations, and the homepage's featured-work slots.</summary>
public interface IContentProofService
{
    Task<Result<ContentDetailResponse>> VerifyAsync(Guid id, VerifyProofRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> RevokeAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> SetFeaturedAsync(Guid id, SetFeaturedRequest request, CancellationToken cancellationToken);
}
