using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Onyx.Contracts.Common;
using Onyx.Web.ApiClients;
using Onyx.Web.Attribution;
using Onyx.Web.Configuration;
using Onyx.Web.Consent;
using Onyx.Web.Localization;
using Onyx.Web.Resources;
using Onyx.Web.Security;

namespace Onyx.Web.Leads;

/// <summary>
/// Shared plumbing for the public conversion pages: SEO metadata, the Turnstile allowance, page context capture and API error
/// display. Reached only through <see cref="LocalizedRouteTransformer"/>.
/// </summary>
[AllowsTurnstile]
public abstract class LeadPageController(
    LocalizedRouteTable routes,
    LanguageCatalog languages,
    SeoContext seo,
    ConsentState consent,
    IOptions<TurnstileOptions> turnstile,
    IStringLocalizer<SharedResource> localizer,
    TimeProvider timeProvider) : Controller
{
    public const string ReferenceKey = "onyx.reference";

    protected ConsentState Consent => consent;

    protected TimeProvider Time => timeProvider;

    protected IStringLocalizer<SharedResource> Text => localizer;

    /// <summary>Page title, canonical and alternates. Confirmation and token pages are never indexed.</summary>
    protected void Describe(string pageKey, string lang, string titleKey, bool noIndex, params string[] slugs)
    {
        var alternates = LeadPages.Alternates(routes, pageKey, languages.Languages.Select(language => language.Code), slugs);
        seo.Title = localizer[titleKey].Value;
        seo.CanonicalPath = alternates.GetValueOrDefault(lang);
        foreach (var (language, path) in alternates)
        {
            seo.Alternates[language] = path;
        }

        seo.NoIndex = noIndex;
        ViewData["TurnstileSiteKey"] = turnstile.Value.IsConfigured ? turnstile.Value.SiteKey : null;
    }

    protected string? PathOf(string pageKey, string lang, params string[] slugs) => routes.PathFor(pageKey, lang, slugs);

    /// <summary>The landing context of this page view, carried in the form so it survives without cookies.</summary>
    protected string? CapturePageContext() =>
        AttributionCookies.CurrentTouch(Request, timeProvider.GetUtcNow()) is { } touch ? AttributionCookies.Encode(touch) : null;

    protected void AddApiError<T>(ApiResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ModelState.AddModelError(string.Empty, localizer[LeadFormErrors.KeyFor(result.Problem, result.IsUnavailable)].Value);
    }

    protected static bool IsProblem(ApiProblem? problem, string code) => string.Equals(problem?.Code, code, StringComparison.Ordinal);
}
