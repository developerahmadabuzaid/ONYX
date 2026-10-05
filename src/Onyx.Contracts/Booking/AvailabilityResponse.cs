namespace Onyx.Contracts.Booking;

/// <summary>
/// Free consultation slots. When <see cref="Bookable"/> is false (booking disabled or not yet configured) there are no slots
/// and the site offers the consultation request form instead.
/// </summary>
public sealed record AvailabilityResponse(bool Bookable, string TimeZoneId, int SlotMinutes, IReadOnlyList<AvailableSlotResponse> Slots);
