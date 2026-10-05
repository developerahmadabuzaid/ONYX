namespace Onyx.Contracts.Booking;

/// <summary><see cref="Outcome"/> is Completed or NoShow.</summary>
public sealed record RecordBookingOutcomeRequest(string Outcome, uint? Version);
