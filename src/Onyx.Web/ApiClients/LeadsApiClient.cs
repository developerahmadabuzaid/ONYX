using Onyx.Contracts.Booking;
using Onyx.Contracts.Leads;

namespace Onyx.Web.ApiClients;

/// <summary>The manage token always travels in a request body, never in an API URL.</summary>
public sealed class LeadsApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), ILeadsApiClient
{
    private const string Consultations = "api/v1/consultations";

    public Task<ApiResult<InquiryAcceptedResponse>> SubmitInquiryAsync(SubmitInquiryRequest request, CancellationToken cancellationToken) =>
        PostAsync<InquiryAcceptedResponse>("api/v1/inquiries", request, cancellationToken);

    public Task<ApiResult<AvailabilityResponse>> GetAvailabilityAsync(CancellationToken cancellationToken) =>
        GetAsync<AvailabilityResponse>($"{Consultations}/availability", cancellationToken);

    public Task<ApiResult<BookingConfirmedResponse>> BookAsync(BookConsultationRequest request, CancellationToken cancellationToken) =>
        PostAsync<BookingConfirmedResponse>(Consultations, request, cancellationToken);

    public Task<ApiResult<ManagedBookingResponse>> GetManagedBookingAsync(string token, CancellationToken cancellationToken) =>
        PostAsync<ManagedBookingResponse>($"{Consultations}/manage", new ManageBookingRequest(token), cancellationToken);

    public Task<ApiResult<ManagedBookingResponse>> CancelBookingAsync(string token, CancellationToken cancellationToken) =>
        PostAsync<ManagedBookingResponse>($"{Consultations}/manage/cancel", new ManageBookingRequest(token), cancellationToken);

    public Task<ApiResult<ManagedBookingResponse>> RescheduleBookingAsync(string token, DateTimeOffset startUtc, CancellationToken cancellationToken) =>
        PostAsync<ManagedBookingResponse>($"{Consultations}/manage/reschedule", new RescheduleBookingRequest(token, startUtc), cancellationToken);

    public Task<ApiResult<CalendarFileResponse>> GetCalendarFileAsync(string token, CancellationToken cancellationToken) =>
        PostAsync<CalendarFileResponse>($"{Consultations}/manage/calendar", new ManageBookingRequest(token), cancellationToken);
}
