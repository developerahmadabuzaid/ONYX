using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Onyx.Contracts.Privacy;
using Onyx.Web.ApiClients;
using Onyx.Web.Configuration;
using Onyx.Web.Consent;

namespace Onyx.Web.Controllers;

/// <summary>Saves cookie choices: a server-side receipt via the API, and the <c>onyx.consent</c> cookie that gates scripts.</summary>
public sealed partial class ConsentController(
    IConsentApiClient consentApi,
    ConsentState consent,
    IOptions<ConsentOptions> options,
    ILogger<ConsentController> logger) : Controller
{
    [HttpPost("/{lang:onyxlang}/consent")]
    public async Task<IActionResult> Save(string lang, string? choice, bool analytics, bool marketing, string? returnUrl, CancellationToken cancellationToken)
    {
        var (allowAnalytics, allowMarketing) = choice switch
        {
            "accept-all" => (true, true),
            "reject-all" => (false, false),
            _ => (analytics, marketing),
        };

        var consentId = consent.ConsentId;
        var version = options.Value.PolicyVersion;
        var receipt = await consentApi.RecordAsync(new RecordCookieConsentRequest(consentId, version, allowAnalytics, allowMarketing, lang), cancellationToken);
        if (!receipt.IsSuccess)
        {
            // The visitor's choice is honoured even if the receipt could not be stored right now.
            LogReceiptFailed(logger, (int)receipt.StatusCode);
        }

        Response.Cookies.Append(ConsentState.CookieName, ConsentState.Format(version, allowAnalytics, allowMarketing, consentId), new CookieOptions
        {
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/",
            MaxAge = TimeSpan.FromDays(options.Value.CookieLifetimeDays),
        });

        if (!allowAnalytics)
        {
            RemoveAnalyticsCookies();
        }

        var safe = returnUrl is not null && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith($"/{lang}", StringComparison.Ordinal);
        return LocalRedirect(safe ? returnUrl! : $"/{lang}/");
    }

    /// <summary>
    /// Withdrawing (or refusing) analytics consent removes GA4's cookies. The tag sets them on this exact host (cookie_domain),
    /// so they are expired both host-only and for the host domain; with consent withdrawn the tag no longer loads at all.
    /// </summary>
    private void RemoveAnalyticsCookies()
    {
        foreach (var name in Request.Cookies.Keys.Where(Analytics.AnalyticsTag.IsAnalyticsCookie))
        {
            Response.Cookies.Delete(name, new CookieOptions { Path = "/", Secure = true, SameSite = SameSiteMode.Lax });
            Response.Cookies.Delete(name, new CookieOptions { Path = "/", Domain = Request.Host.Host, Secure = true, SameSite = SameSiteMode.Lax });
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cookie consent receipt could not be recorded (status {StatusCode})")]
    private static partial void LogReceiptFailed(ILogger logger, int statusCode);
}
