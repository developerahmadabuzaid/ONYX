using Onyx.Contracts.Knowledge;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record KnowledgeListViewModel(
    IReadOnlyList<KnowledgeEntryResponse> Entries, IReadOnlyList<InformationRequestResponse> InformationRequests, GovernanceRulesResponse Rules, string? Status);
