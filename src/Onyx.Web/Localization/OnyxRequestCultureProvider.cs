using Microsoft.AspNetCore.Localization;

namespace Onyx.Web.Localization;

/// <summary>
/// Public pages: the language is the <c>{lang}</c> route value, nothing else (no Accept-Language, no cookie fallback).
/// Admin pages: the signed-in admin's preferred language, else the visitor's explicit choice, else the default (Arabic).
/// </summary>
public sealed class OnyxRequestCultureProvider(LanguageCatalog languages) : RequestCultureProvider
{
    public const string LanguageClaim = "lang";

    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        string? language;

        if (httpContext.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
        {
            language = httpContext.User.FindFirst(LanguageClaim)?.Value;
            if (!languages.IsSupported(language))
            {
                language = httpContext.Request.Cookies[LanguageCatalog.ChoiceCookieName];
            }
        }
        else
        {
            language = httpContext.GetRouteValue("lang") as string;
        }

        language = languages.IsSupported(language) ? language : languages.DefaultLanguage;
        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(language, language));
    }
}
