namespace Onyx.Contracts.Booking;

/// <summary>A free slot. <see cref="StartLocal"/> is the same instant in the business time zone, for display.</summary>
public sealed record AvailableSlotResponse(DateTimeOffset StartUtc, DateTimeOffset EndUtc, DateTimeOffset StartLocal);
