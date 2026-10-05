namespace Onyx.Contracts.Leads;

public sealed record InquiryResponse(
    Guid Id,
    Guid LeadId,
    string Type,
    string LanguageCode,
    string Name,
    string Email,
    string? Phone,
    string? Company,
    string? Country,
    string? Message,
    int AnswersVersion,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Answers,
    Guid? ServiceId,
    Guid? ProjectId,
    Guid? CallToActionId,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset VerifiedAtUtc,
    OriginResponse Origin,
    ConsentResponse? Consent);
