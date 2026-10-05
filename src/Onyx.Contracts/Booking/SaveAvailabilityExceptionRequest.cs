namespace Onyx.Contracts.Booking;

public sealed record SaveAvailabilityExceptionRequest(DateOnly Date, string? Reason);
