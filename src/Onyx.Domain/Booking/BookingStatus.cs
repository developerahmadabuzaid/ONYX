namespace Onyx.Domain.Booking;

/// <summary>
/// Consultation booking states (architecture.md §13.4). Requested is reserved for a future approval-first calendar provider;
/// the internal provider confirms bookable slots immediately. Rescheduled marks the old booking replaced by a new one.
/// </summary>
public enum BookingStatus
{
    Requested = 0,
    Confirmed = 1,
    Rescheduled = 2,
    Cancelled = 3,
    Completed = 4,
    NoShow = 5,
}
