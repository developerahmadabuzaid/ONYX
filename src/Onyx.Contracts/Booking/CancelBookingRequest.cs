namespace Onyx.Contracts.Booking;

/// <summary>Staff cancellation. <see cref="NotifyVisitor"/> sends the visitor a cancellation email in the booking language.</summary>
public sealed record CancelBookingRequest(bool NotifyVisitor, uint? Version);
