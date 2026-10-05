namespace Onyx.Contracts.Leads;

public sealed record LeadBookingResponse(Guid Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string TimeZoneId, string Status);
