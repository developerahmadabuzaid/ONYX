namespace Onyx.Contracts.Booking;

public sealed record AvailabilityExceptionResponse(Guid Id, DateOnly Date, string? Reason);
