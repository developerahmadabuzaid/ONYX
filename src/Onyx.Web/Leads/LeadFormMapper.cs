using Onyx.Contracts.Leads;
using Onyx.Web.Attribution;
using Onyx.Web.Consent;

namespace Onyx.Web.Leads;

/// <summary>Turns a validated form into the API request, adding the Turnstile token and consent-aware attribution.</summary>
public static class LeadFormMapper
{
    /// <summary>Field the Turnstile widget posts its token in.</summary>
    public const string TurnstileField = "cf-turnstile-response";

    public static InquiryFormDto ToDto(
        InquiryFormModel form, string languageCode, int answersVersion, IReadOnlyDictionary<string, IReadOnlyList<string>>? answers, HttpRequest request, ConsentState consent)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(consent);
        var (first, last) = AttributionCookies.Read(request, consent);
        var pageContext = AttributionCookies.Decode(form.Context);
        var attribution = new AttributionDto(
            first,
            last ?? pageContext,
            request.Path.Value,
            null,
            AttributionCookies.DeviceClass(request),
            AttributionCookies.AnalyticsClientId(request, consent),
            consent.AnalyticsAllowed);

        return new InquiryFormDto(
            languageCode, form.Name ?? string.Empty, form.Email ?? string.Empty, Blank(form.Phone), Blank(form.Company), Blank(form.Country), Blank(form.Message),
            answersVersion, answers, form.ServiceId, form.ProjectId, form.CtaId, form.ContactConsent, form.MarketingConsent,
            request.HasFormContentType ? request.Form[TurnstileField].ToString() : null, form.Website, attribution);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
