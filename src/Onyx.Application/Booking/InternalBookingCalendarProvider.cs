using Onyx.Domain.Booking;

namespace Onyx.Application.Booking;

/// <summary>The default: bookings live only in the Onyx database (admin Bookings screen, emails with a calendar file).</summary>
public sealed class InternalBookingCalendarProvider : IBookingCalendarProvider
{
    public Task BookingConfirmedAsync(ConsultationBooking booking, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task BookingCancelledAsync(ConsultationBooking booking, CancellationToken cancellationToken) => Task.CompletedTask;
}
