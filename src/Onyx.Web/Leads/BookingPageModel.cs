using Onyx.Contracts.Booking;

namespace Onyx.Web.Leads;

/// <summary>The booking form plus the free slots; <see cref="Availability"/> is null when only a request can be made.</summary>
public sealed record BookingPageModel(BookingFormModel Form, AvailabilityResponse? Availability);
