using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Onyx.Web.ApiClients;
using Onyx.Web.Configuration;
using Onyx.Web.Content;
using Onyx.Web.Content.SiteContent;
using Onyx.Web.Localization;
using Onyx.Web.Resources;

namespace Onyx.Web.Controllers;

public sealed class HomeController(
    LanguageCatalog languages,
    SeoContext seo,
    IPublicContentApiClient api,
    IPublicNewsApiClient news,
    LocalizedRouteTable routes,
    IOptions<SiteOptions> site,
    IStringLocalizer<SharedResource> localizer,
    SiteCopy copy) : Controller
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
        // The Admin preview renders this action under its own /admin URL, so the trailing-slash rule does not apply there.
        if (!copy.IsPreview && Request.Path.Value is { } path && !path.EndsWith('/'))
        {
            return RedirectPermanent($"/{lang}/{Request.QueryString}");
        }

        // Editable in Admin → Homepage (SEO group); the resource files are the fallback.
        seo.Title = copy["Home.Seo.Title"];
        seo.Description = copy["Home.Seo.Description"];
        seo.OpenGraphTitle = copy["Home.Seo.OgTitle"];
        seo.OpenGraphDescription = seo.Description;
        seo.OpenGraphImagePath = SiteCopy.MediaPath(copy.HomeSettings.SocialImageMediaId);
        seo.CanonicalPath = $"/{lang}/";
        foreach (var language in languages.Languages)
        {
            seo.Alternates[language.Code] = $"/{language.Code}/";
        }

        // The four reads are independent, so they run concurrently: the page waits for the slowest, not for the sum.
        // FeaturedWork: at most three live, verified projects, hidden entirely when there are none.
        // LatestNews (architecture.md §7): up to four published News Hub items, hidden when there are none.
        // LatestInsights: the newest three published insights (first listing page), hidden when there are none.
        var organization = api.GetOrganizationAsync(cancellationToken);
        var featured = api.GetFeaturedProjectsAsync(lang, cancellationToken);
        var latestNews = news.GetLatestAsync(lang, cancellationToken);
        var insights = api.GetLatestInsightsAsync(lang, 3, cancellationToken);
        await Task.WhenAll(organization, featured, latestNews, insights);

        seo.StructuredDataJson = StructuredDataBuilder.BuildHome(lang, site.Value.PublicBaseUrl, localizer["Site.Name"].Value, await organization);
        // Explicit path: the admin preview renders this same view from the Admin area.
        return View("~/Views/Home/Index.cshtml", new HomePageModel(await featured, await latestNews, routes.PathFor(News.NewsPages.Hub, lang), await insights));
    }
}
