using Onyx.Contracts.Booking;
using Onyx.Domain.Common;

namespace Onyx.Application.Booking;

/// <summary>Public consultation booking and the visitor manage link (cancel, reschedule, calendar file).</summary>
public interface IBookingService
{
    Task<AvailabilityResponse> GetAvailabilityAsync(CancellationToken cancellationToken);

    Task<Result<BookingConfirmedResponse>> BookAsync(BookConsultationRequest request, CancellationToken cancellationToken);

    Task<Result<ManagedBookingResponse>> GetManagedAsync(ManageBookingRequest request, CancellationToken cancellationToken);

    Task<Result<ManagedBookingResponse>> CancelAsync(ManageBookingRequest request, CancellationToken cancellationToken);

    Task<Result<ManagedBookingResponse>> RescheduleAsync(RescheduleBookingRequest request, CancellationToken cancellationToken);

    Task<Result<CalendarFileResponse>> GetCalendarFileAsync(ManageBookingRequest request, CancellationToken cancellationToken);
}
