using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>
/// A form that passed validation and the anti-abuse check, ready to be stored. <see cref="IsSpam"/> marks a filled honeypot:
/// the caller reports success to the client but stores nothing.
/// </summary>
internal sealed record PreparedInquiry(
    bool IsSpam,
    InquiryType Type,
    string LanguageCode,
    ContactDetails? Contact,
    string? Message,
    InquiryAnswers Answers,
    Guid? ServiceId,
    Guid? ProjectId,
    Guid? CallToActionId,
    Guid? PillarId,
    Attribution? Attribution,
    bool MarketingConsent)
{
    public static PreparedInquiry Spam(InquiryType type, string languageCode) =>
        new(true, type, languageCode, null, null, InquiryAnswers.Empty, null, null, null, null, null, false);
}
