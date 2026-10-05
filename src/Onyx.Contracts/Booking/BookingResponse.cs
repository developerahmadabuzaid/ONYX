namespace Onyx.Contracts.Booking;

public sealed record BookingResponse(
    Guid Id,
    Guid LeadId,
    string LeadName,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    DateTimeOffset StartLocal,
    string TimeZoneId,
    string Status,
    string LanguageCode,
    Guid? RescheduledFromId,
    Guid? RescheduledToId,
    DateTimeOffset? CancelledAtUtc,
    bool CancelledByVisitor,
    uint Version);
