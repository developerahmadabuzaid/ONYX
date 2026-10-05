namespace Onyx.Application.Booking;

/// <summary>Provider-neutral availability: whether booking is offered, the business time zone and the free slots.</summary>
public sealed record AvailabilitySnapshot(bool Bookable, string TimeZoneId, int SlotMinutes, IReadOnlyList<BookableSlot> Slots)
{
    public static AvailabilitySnapshot Closed(string timeZoneId) => new(false, timeZoneId, 0, []);
}
