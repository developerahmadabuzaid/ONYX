using Onyx.Domain.Booking;

namespace Onyx.Application.Booking;

/// <summary>
/// Mirrors bookings into an external calendar (architecture.md §13.4). The default internal provider keeps bookings only in the
/// Onyx database and does nothing here; calendar products (Google, Microsoft 365, ...) are later adapters, enabled only when
/// approved and configured. Called by the Worker, never inside a visitor request.
/// </summary>
public interface IBookingCalendarProvider
{
    Task BookingConfirmedAsync(ConsultationBooking booking, CancellationToken cancellationToken);

    Task BookingCancelledAsync(ConsultationBooking booking, CancellationToken cancellationToken);
}
