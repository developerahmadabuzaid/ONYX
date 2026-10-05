using Onyx.Contracts.Leads;
using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

internal static class LeadMapping
{
    public static LeadSummaryResponse ToSummary(Lead lead, IReadOnlyDictionary<Guid, string> userNames) =>
        new(
            lead.Id, lead.DisplayName, lead.Company, lead.Stage.ToString(), lead.Source.ToString(), lead.Priority.ToString(), lead.Score, lead.OwnerId,
            lead.OwnerId is { } owner && userNames.TryGetValue(owner, out var name) ? name : null, lead.InquiryCount, lead.LanguageCode,
            lead.FirstInquiryAtUtc, lead.LastInquiryAtUtc, lead.Version);

    public static InquiryResponse ToResponse(Inquiry inquiry, ConsentRecord? consent) =>
        new(
            inquiry.Id, inquiry.LeadId, inquiry.Type.ToString(), inquiry.LanguageCode, inquiry.Name, inquiry.Email, inquiry.Phone, inquiry.Company,
            inquiry.Country, inquiry.Message, inquiry.Answers.Version, inquiry.Answers.Values, inquiry.ServiceId, inquiry.ProjectId, inquiry.CallToActionId,
            inquiry.SubmittedAtUtc, inquiry.VerifiedAtUtc, ToOrigin(inquiry.Attribution), consent is null ? null : ToResponse(consent));

    public static ConsentResponse ToResponse(ConsentRecord consent) =>
        new(consent.Id, consent.ContactConsent, consent.MarketingConsent, consent.HasMarketingConsent, consent.ConsentVersion, consent.GivenAtUtc, consent.MarketingWithdrawnAtUtc);

    public static LeadActivityResponse ToResponse(LeadActivity activity) =>
        new(activity.Id, activity.Kind.ToString(), activity.Detail, activity.RelatedId, activity.ActorId, activity.OccurredAtUtc);

    private static OriginResponse ToOrigin(Attribution attribution) =>
        new(
            ToDto(attribution.FirstTouch), ToDto(attribution.LastTouch), attribution.SubmissionPath, attribution.LanguageCode, attribution.Market,
            attribution.DeviceClass, attribution.ToolSource, attribution.ToolRunId, attribution.AnalyticsClientId, attribution.AnalyticsConsent);

    private static TouchPointDto? ToDto(TouchPoint? touch) =>
        touch is null
            ? null
            : new TouchPointDto(touch.LandingPath, touch.Referrer, touch.UtmSource, touch.UtmMedium, touch.UtmCampaign, touch.UtmContent, touch.UtmTerm, touch.AtUtc);
}
