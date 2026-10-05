namespace Onyx.Contracts.Leads;

/// <summary>
/// Fields shared by every public inquiry form. <see cref="CaptchaToken"/> is the Turnstile response, verified server-side;
/// <see cref="Website"/> is the honeypot field, which people never see and must leave empty.
/// </summary>
public sealed record InquiryFormDto(
    string LanguageCode,
    string Name,
    string Email,
    string? Phone,
    string? Company,
    string? Country,
    string? Message,
    int AnswersVersion,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Answers,
    Guid? ServiceId,
    Guid? ProjectId,
    Guid? CallToActionId,
    bool ContactConsent,
    bool MarketingConsent,
    string? CaptchaToken,
    string? Website,
    AttributionDto? Attribution);
