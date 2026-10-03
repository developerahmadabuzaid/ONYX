using Microsoft.AspNetCore.Mvc;
using Onyx.Web.Localization;

namespace Onyx.Web.Controllers;

public sealed class HomeController(LanguageCatalog languages, SeoContext seo) : Controller
{
    /// <summary><c>/</c> → Arabic, unless the visitor explicitly chose another language. Accept-Language never redirects.</summary>
    [HttpGet("/")]
    public IActionResult Root()
    {
        var choice = Request.Cookies[LanguageCatalog.ChoiceCookieName];
        var language = languages.IsSupported(choice) ? choice! : languages.DefaultLanguage;
        return Redirect($"/{language}/");
    }

    /// <summary>Home shell only: page content arrives with the CMS (Stage 6) and visual design after Stage 12.</summary>
    [HttpGet("/{lang:onyxlang}/")]
    public IActionResult Index(string lang)
    {
        seo.CanonicalPath = $"/{lang}/";
        foreach (var language in languages.Languages)
        {
            seo.Alternates[language.Code] = $"/{language.Code}/";
        }

        return View();
    }
}
