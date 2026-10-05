namespace Onyx.Contracts.Booking;

public sealed record BookingConfirmedResponse(string Reference, DateTimeOffset StartUtc, DateTimeOffset EndUtc, DateTimeOffset StartLocal, string TimeZoneId);
