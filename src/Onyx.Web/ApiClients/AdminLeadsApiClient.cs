using System.Globalization;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;

namespace Onyx.Web.ApiClients;

public sealed class AdminLeadsApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IAdminLeadsApiClient
{
    private const string Leads = "api/v1/admin/leads";
    private const string Bookings = "api/v1/admin/bookings";
    private const string Availability = "api/v1/admin/availability";

    public Task<ApiResult<PagedResult<LeadSummaryResponse>>> ListLeadsAsync(LeadListQuery query, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<LeadSummaryResponse>>($"{Leads}?{LeadQuery(query)}", cancellationToken);

    public Task<ApiResult<LeadDetailResponse>> GetLeadAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<LeadDetailResponse>($"{Leads}/{id:D}", cancellationToken);

    public Task<ApiResult<LeadSummaryResponse>> ChangeStageAsync(Guid id, ChangeLeadStageRequest request, CancellationToken cancellationToken) =>
        PutAsync<LeadSummaryResponse>($"{Leads}/{id:D}/stage", request, cancellationToken);

    public Task<ApiResult<LeadSummaryResponse>> ChangePriorityAsync(Guid id, ChangeLeadPriorityRequest request, CancellationToken cancellationToken) =>
        PutAsync<LeadSummaryResponse>($"{Leads}/{id:D}/priority", request, cancellationToken);

    public Task<ApiResult<LeadSummaryResponse>> AssignAsync(Guid id, AssignLeadRequest request, CancellationToken cancellationToken) =>
        PutAsync<LeadSummaryResponse>($"{Leads}/{id:D}/owner", request, cancellationToken);

    public Task<ApiResult<LeadNoteResponse>> AddNoteAsync(Guid id, AddLeadNoteRequest request, CancellationToken cancellationToken) =>
        PostAsync<LeadNoteResponse>($"{Leads}/{id:D}/notes", request, cancellationToken);

    public Task<ApiResult<NoContent>> WithdrawMarketingConsentAsync(Guid id, Guid consentId, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"{Leads}/{id:D}/consents/{consentId:D}/withdraw-marketing", null, cancellationToken);

    public Task<ApiResult<IReadOnlyList<LeadAssigneeResponse>>> ListAssigneesAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<LeadAssigneeResponse>>($"{Leads}/assignees", cancellationToken);

    public Task<ApiFileStream?> ExportLeadsAsync(LeadListQuery query, CancellationToken cancellationToken) =>
        GetStreamAsync($"{Leads}/export?{LeadQuery(query)}", cancellationToken);

    public Task<ApiResult<PagedResult<InquirySummaryResponse>>> ListInquiriesAsync(InquiryListQuery query, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<InquirySummaryResponse>>(
            $"api/v1/admin/inquiries?page={Number(query.Page)}&pageSize={Number(query.PageSize)}{Query("type", query.Type)}", cancellationToken);

    public Task<ApiResult<PagedResult<BookingResponse>>> ListBookingsAsync(BookingListQuery query, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<BookingResponse>>(
            $"{Bookings}?upcoming={(query.Upcoming ? "true" : "false")}&page={Number(query.Page)}&pageSize={Number(query.PageSize)}{Query("status", query.Status)}",
            cancellationToken);

    public Task<ApiResult<BookingResponse>> CancelBookingAsync(Guid id, CancelBookingRequest request, CancellationToken cancellationToken) =>
        PostAsync<BookingResponse>($"{Bookings}/{id:D}/cancel", request, cancellationToken);

    public Task<ApiResult<BookingResponse>> RecordBookingOutcomeAsync(Guid id, RecordBookingOutcomeRequest request, CancellationToken cancellationToken) =>
        PostAsync<BookingResponse>($"{Bookings}/{id:D}/outcome", request, cancellationToken);

    public Task<ApiResult<BookingSettingsResponse>> GetAvailabilityAsync(CancellationToken cancellationToken) =>
        GetAsync<BookingSettingsResponse>(Availability, cancellationToken);

    public Task<ApiResult<BookingSettingsResponse>> SaveBookingSettingsAsync(SaveBookingSettingsRequest request, CancellationToken cancellationToken) =>
        PutAsync<BookingSettingsResponse>($"{Availability}/settings", request, cancellationToken);

    public Task<ApiResult<BookingSettingsResponse>> AddAvailabilityRuleAsync(SaveAvailabilityRuleRequest request, CancellationToken cancellationToken) =>
        PostAsync<BookingSettingsResponse>($"{Availability}/rules", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteAvailabilityRuleAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"{Availability}/rules/{id:D}", cancellationToken);

    public Task<ApiResult<BookingSettingsResponse>> AddAvailabilityExceptionAsync(SaveAvailabilityExceptionRequest request, CancellationToken cancellationToken) =>
        PostAsync<BookingSettingsResponse>($"{Availability}/exceptions", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteAvailabilityExceptionAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"{Availability}/exceptions/{id:D}", cancellationToken);

    private static string LeadQuery(LeadListQuery query) =>
        $"page={Number(query.Page)}&pageSize={Number(query.PageSize)}{Query("stage", query.Stage)}{Query("owner", query.Owner)}{Query("search", query.Search)}";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Query(string name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"&{name}={Uri.EscapeDataString(value.Trim())}";
}
