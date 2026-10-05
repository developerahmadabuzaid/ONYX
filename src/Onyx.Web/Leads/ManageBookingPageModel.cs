using Onyx.Contracts.Booking;

namespace Onyx.Web.Leads;

/// <summary>The manage page. <see cref="Booking"/> is null when the link is invalid or the booking is no longer active.</summary>
public sealed record ManageBookingPageModel(string Token, ManagedBookingResponse? Booking, AvailabilityResponse? Availability, string? NoticeKey);
