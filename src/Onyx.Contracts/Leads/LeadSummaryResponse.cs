namespace Onyx.Contracts.Leads;

public sealed record LeadSummaryResponse(
    Guid Id,
    string DisplayName,
    string? Company,
    string Stage,
    string Source,
    string Priority,
    int Score,
    Guid? OwnerId,
    string? OwnerName,
    int InquiryCount,
    string LanguageCode,
    DateTimeOffset FirstInquiryAtUtc,
    DateTimeOffset LastInquiryAtUtc,
    uint Version);
