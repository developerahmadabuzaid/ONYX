using Onyx.Contracts.Content;
using Onyx.Contracts.Proof;
using Onyx.Domain.Common;

namespace Onyx.Application.Proof;

/// <summary>Capabilities, metrics and testimonials of projects and case studies.</summary>
public interface IProofItemService
{
    Task<Result<IReadOnlyList<ProofItemResponse>>> ListAsync(Guid contentItemId, CancellationToken cancellationToken);

    Task<Result<ProofItemResponse>> CreateAsync(SaveProofItemRequest request, CancellationToken cancellationToken);

    Task<Result<ProofItemResponse>> UpdateAsync(Guid id, SaveProofItemRequest request, CancellationToken cancellationToken);

    Task<Result<ProofItemResponse>> VerifyAsync(Guid id, VerifyProofRequest request, CancellationToken cancellationToken);

    Task<Result<ProofItemResponse>> PublishAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken);

    Task<Result<ProofItemResponse>> UnpublishAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
