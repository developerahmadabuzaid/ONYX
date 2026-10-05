namespace Onyx.Contracts.Booking;

/// <summary><see cref="Upcoming"/> lists confirmed bookings from now on; otherwise all bookings, newest first.</summary>
public sealed record BookingListQuery(bool Upcoming = true, string? Status = null, int Page = 1, int PageSize = 25);
