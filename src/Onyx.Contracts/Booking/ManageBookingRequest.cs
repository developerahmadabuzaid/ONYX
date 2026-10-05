namespace Onyx.Contracts.Booking;

/// <summary>The token from the visitor manage link. Sent in the body, never in a URL the API logs.</summary>
public sealed record ManageBookingRequest(string Token);
