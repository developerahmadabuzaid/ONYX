using Microsoft.AspNetCore.Mvc;
using Onyx.Web.Localization;

namespace Onyx.Web.Controllers;

/// <summary>Records an explicit language choice and moves to the equivalent page in that language.</summary>
public sealed class LanguageController : Controller
{
    [HttpGet("/{lang:onyxlang}/language")]
    public IActionResult Switch(string lang, [FromQuery(Name = "return")] string? returnPath)
    {
        // A per-visitor redirect that sets a cookie: never cached, and not a page for crawlers (robots.txt disallows it after launch).
        Response.Headers.CacheControl = "private, no-store";
        Response.Cookies.Append(LanguageCatalog.ChoiceCookieName, lang, new CookieOptions
        {
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/",
            MaxAge = TimeSpan.FromDays(365),
        });

        // Only same-site paths in the chosen language: no open redirect.
        var safe = returnPath is not null && Url.IsLocalUrl(returnPath)
            && (returnPath == $"/{lang}" || returnPath.StartsWith($"/{lang}/", StringComparison.Ordinal));
        return LocalRedirect(safe ? returnPath! : $"/{lang}/");
    }
}
