namespace Onyx.Contracts.Booking;

/// <summary>What the manage page shows. No contact details: the link may be forwarded.</summary>
public sealed record ManagedBookingResponse(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    DateTimeOffset StartLocal,
    string TimeZoneId,
    string Status,
    string LanguageCode,
    bool CanManage);
