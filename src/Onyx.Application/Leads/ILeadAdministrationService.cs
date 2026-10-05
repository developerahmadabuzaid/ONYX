using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;
using Onyx.Domain.Common;

namespace Onyx.Application.Leads;

/// <summary>
/// The Admin lead pipeline. Visibility: <c>Leads.ViewAll</c> sees every lead; <c>Leads.View</c> sees leads it owns and
/// unassigned leads. A lead outside the caller's visibility is reported as not found.
/// </summary>
public interface ILeadAdministrationService
{
    Task<Result<PagedResult<LeadSummaryResponse>>> ListAsync(LeadListQuery query, CancellationToken cancellationToken);

    Task<Result<LeadDetailResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<LeadSummaryResponse>> ChangeStageAsync(Guid id, ChangeLeadStageRequest request, CancellationToken cancellationToken);

    Task<Result<LeadSummaryResponse>> ChangePriorityAsync(Guid id, ChangeLeadPriorityRequest request, CancellationToken cancellationToken);

    Task<Result<LeadSummaryResponse>> AssignAsync(Guid id, AssignLeadRequest request, CancellationToken cancellationToken);

    Task<Result<LeadNoteResponse>> AddNoteAsync(Guid id, AddLeadNoteRequest request, CancellationToken cancellationToken);

    Task<Result> WithdrawMarketingConsentAsync(Guid id, Guid consentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeadAssigneeResponse>> ListAssigneesAsync(CancellationToken cancellationToken);

    Task<Result<PagedResult<InquirySummaryResponse>>> ListInquiriesAsync(InquiryListQuery query, CancellationToken cancellationToken);

    Task<Result<LeadExportFile>> ExportAsync(LeadListQuery query, CancellationToken cancellationToken);
}
