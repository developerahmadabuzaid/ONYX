using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Onyx.Web.ApiClients;
using Onyx.Web.Configuration;
using Onyx.Web.Content;
using Onyx.Web.Localization;
using Onyx.Web.Resources;

namespace Onyx.Web.Controllers;

public sealed class HomeController(
    LanguageCatalog languages, SeoContext seo, IPublicContentApiClient api, IOptions<SiteOptions> site, IStringLocalizer<SharedResource> localizer) : Controller
{
    /// <summary>
    /// <c>/</c> → Arabic, unless the visitor explicitly chose another language. Accept-Language never redirects. The answer
    /// depends on a cookie, so it must never be cached by a browser, proxy or CDN for other visitors.
    /// </summary>
    [HttpGet("/")]
    public IActionResult Root()
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.Vary = "Cookie";
        var choice = Request.Cookies[LanguageCatalog.ChoiceCookieName];
        var language = languages.IsSupported(choice) ? choice! : languages.DefaultLanguage;
        return Redirect($"/{language}/");
    }

    /// <summary>Home shell only: page content arrives later and visual design after Stage 12. <c>/ar</c> → 301 <c>/ar/</c> (one URL per page).</summary>
    [HttpGet("/{lang:onyxlang}/")]
    public async Task<IActionResult> Index(string lang, CancellationToken cancellationToken)
    {
        if (Request.Path.Value is { } path && !path.EndsWith('/'))
        {
            return RedirectPermanent($"/{lang}/{Request.QueryString}");
        }

        seo.CanonicalPath = $"/{lang}/";
        foreach (var language in languages.Languages)
        {
            seo.Alternates[language.Code] = $"/{language.Code}/";
        }

        var organization = await api.GetOrganizationAsync(cancellationToken);
        seo.StructuredDataJson = StructuredDataBuilder.BuildHome(lang, site.Value.PublicBaseUrl, localizer["Site.Name"].Value, organization);

        // FeaturedWork: at most three live, verified projects, hidden entirely when there are none.
        return View(await api.GetFeaturedProjectsAsync(lang, cancellationToken));
    }
}
