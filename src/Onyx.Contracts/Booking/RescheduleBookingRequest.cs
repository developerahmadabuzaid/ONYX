namespace Onyx.Contracts.Booking;

public sealed record RescheduleBookingRequest(string Token, DateTimeOffset StartUtc);
