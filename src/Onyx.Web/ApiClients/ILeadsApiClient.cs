using Onyx.Contracts.Booking;
using Onyx.Contracts.Leads;

namespace Onyx.Web.ApiClients;

/// <summary>Public conversion endpoints: inquiry forms, consultation availability and booking, and the visitor manage link.</summary>
public interface ILeadsApiClient
{
    Task<ApiResult<InquiryAcceptedResponse>> SubmitInquiryAsync(SubmitInquiryRequest request, CancellationToken cancellationToken);

    Task<ApiResult<AvailabilityResponse>> GetAvailabilityAsync(CancellationToken cancellationToken);

    Task<ApiResult<BookingConfirmedResponse>> BookAsync(BookConsultationRequest request, CancellationToken cancellationToken);

    Task<ApiResult<ManagedBookingResponse>> GetManagedBookingAsync(string token, CancellationToken cancellationToken);

    Task<ApiResult<ManagedBookingResponse>> CancelBookingAsync(string token, CancellationToken cancellationToken);

    Task<ApiResult<ManagedBookingResponse>> RescheduleBookingAsync(string token, DateTimeOffset startUtc, CancellationToken cancellationToken);

    Task<ApiResult<CalendarFileResponse>> GetCalendarFileAsync(string token, CancellationToken cancellationToken);
}
