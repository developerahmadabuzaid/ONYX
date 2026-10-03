using Microsoft.Net.Http.Headers;

namespace Onyx.Web.Localization;

/// <summary>
/// Instead of redirecting on Accept-Language, offers a dismissible link when the browser prefers another supported language
/// and the visitor has not made an explicit choice.
/// </summary>
public sealed class LanguageSuggestion(LanguageCatalog languages)
{
    public string? Suggest(HttpContext httpContext, string currentLanguage)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (httpContext.Request.Cookies.ContainsKey(LanguageCatalog.ChoiceCookieName))
        {
            return null;
        }

        var preferred = StringWithQualityHeaderValue.ParseList(httpContext.Request.Headers.AcceptLanguage.ToArray()!)
            .OrderByDescending(value => value.Quality ?? 1)
            .Select(value => value.Value.ToString().Split('-')[0].ToLowerInvariant())
            .FirstOrDefault();

        return preferred is not null && preferred != currentLanguage && languages.IsSupported(preferred) ? preferred : null;
    }
}
