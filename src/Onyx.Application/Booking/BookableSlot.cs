namespace Onyx.Application.Booking;

/// <summary>A free slot; <see cref="StartLocal"/> is the same instant in the business time zone.</summary>
public sealed record BookableSlot(DateTimeOffset StartUtc, DateTimeOffset EndUtc, DateTimeOffset StartLocal);
