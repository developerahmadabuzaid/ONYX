using Onyx.Contracts.Booking;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;

namespace Onyx.Web.ApiClients;

/// <summary>Admin leads, inquiries, bookings and availability. The API enforces every permission; Web only hides UI.</summary>
public interface IAdminLeadsApiClient
{
    Task<ApiResult<PagedResult<LeadSummaryResponse>>> ListLeadsAsync(LeadListQuery query, CancellationToken cancellationToken);

    Task<ApiResult<LeadDetailResponse>> GetLeadAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<LeadSummaryResponse>> ChangeStageAsync(Guid id, ChangeLeadStageRequest request, CancellationToken cancellationToken);

    Task<ApiResult<LeadSummaryResponse>> ChangePriorityAsync(Guid id, ChangeLeadPriorityRequest request, CancellationToken cancellationToken);

    Task<ApiResult<LeadSummaryResponse>> AssignAsync(Guid id, AssignLeadRequest request, CancellationToken cancellationToken);

    Task<ApiResult<LeadNoteResponse>> AddNoteAsync(Guid id, AddLeadNoteRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> WithdrawMarketingConsentAsync(Guid id, Guid consentId, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<LeadAssigneeResponse>>> ListAssigneesAsync(CancellationToken cancellationToken);

    /// <summary>The CSV export stream, or null when the API refused (e.g. missing permission).</summary>
    Task<ApiFileStream?> ExportLeadsAsync(LeadListQuery query, CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<InquirySummaryResponse>>> ListInquiriesAsync(InquiryListQuery query, CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<BookingResponse>>> ListBookingsAsync(BookingListQuery query, CancellationToken cancellationToken);

    Task<ApiResult<BookingResponse>> CancelBookingAsync(Guid id, CancelBookingRequest request, CancellationToken cancellationToken);

    Task<ApiResult<BookingResponse>> RecordBookingOutcomeAsync(Guid id, RecordBookingOutcomeRequest request, CancellationToken cancellationToken);

    Task<ApiResult<BookingSettingsResponse>> GetAvailabilityAsync(CancellationToken cancellationToken);

    Task<ApiResult<BookingSettingsResponse>> SaveBookingSettingsAsync(SaveBookingSettingsRequest request, CancellationToken cancellationToken);

    Task<ApiResult<BookingSettingsResponse>> AddAvailabilityRuleAsync(SaveAvailabilityRuleRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteAvailabilityRuleAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<BookingSettingsResponse>> AddAvailabilityExceptionAsync(SaveAvailabilityExceptionRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteAvailabilityExceptionAsync(Guid id, CancellationToken cancellationToken);
}
