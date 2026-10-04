using Onyx.Contracts.Knowledge;
using Onyx.Domain.Common;

namespace Onyx.Application.Knowledge;

public interface IKnowledgeService
{
    Task<Result<IReadOnlyList<KnowledgeEntryResponse>>> ListAsync(string? category, string? status, string? search, CancellationToken cancellationToken);

    Task<Result<KnowledgeEntryResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<KnowledgeEntryResponse>> CreateAsync(SaveKnowledgeEntryRequest request, CancellationToken cancellationToken);

    Task<Result<KnowledgeEntryResponse>> UpdateAsync(Guid id, SaveKnowledgeEntryRequest request, CancellationToken cancellationToken);

    Task<Result<KnowledgeEntryResponse>> ApproveAsync(Guid id, KnowledgeVersionRequest request, CancellationToken cancellationToken);

    Task<Result<KnowledgeEntryResponse>> RetireAsync(Guid id, KnowledgeVersionRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<InformationRequestResponse>>> ListRequestsAsync(string? status, CancellationToken cancellationToken);

    Task<Result<InformationRequestResponse>> RaiseRequestAsync(RaiseInformationRequest request, CancellationToken cancellationToken);

    Task<Result<InformationRequestResponse>> ResolveRequestAsync(Guid id, ResolveInformationRequest request, CancellationToken cancellationToken);

    GovernanceRulesResponse GetGovernanceRules();
}
