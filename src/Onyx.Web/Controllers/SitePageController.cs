using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Onyx.Web.Configuration;
using Onyx.Web.Content;
using Onyx.Web.Content.SiteContent;
using Onyx.Web.Content.SitePages;
using Onyx.Web.Localization;
using Onyx.Web.Resources;

namespace Onyx.Web.Controllers;

/// <summary>
/// Code-owned public pages (services, company and legal pages) from <see cref="SitePageCatalog"/>. No API call: the copy ships
/// with the site, so these pages stay fast and available even when the API is not. Drafts are rendered but never indexed.
/// </summary>
public sealed class SitePageController(
    SitePageCatalog catalog,
    EffectiveSitePages effective,
    LanguageCatalog languages,
    SeoContext seo,
    IOptions<SiteOptions> site,
    IStringLocalizer<SharedResource> localizer,
    SiteContentContext context) : Controller
{
    [HttpGet]
    public IActionResult Show(string lang, string page)
    {
        // Editor overrides (published, or a previewed draft) overlay the code-owned page; paths always come from the catalog.
        var definition = effective.Find(page);
        if (definition is null || !definition.Translations.TryGetValue(lang, out var text))
        {
            return NotFound();
        }

        var path = catalog.PathFor(definition.Key, lang)!;
        seo.Title = text.Title;
        seo.Description = text.Description;
        var settings = effective.Settings(definition.Key);
        var textOverride = OverrideText(definition.Key, lang);
        seo.OpenGraphTitle = Blank(textOverride?.OgTitle) ?? text.Title;
        seo.OpenGraphDescription = Blank(textOverride?.OgDescription) ?? text.Description;
        seo.OpenGraphImagePath = SiteCopy.MediaPath(settings.SocialImageMediaId);
        seo.CanonicalPath = path;
        seo.NoIndex = definition.Draft || settings.NoIndex;
        ViewData["HeroImage"] = Guid.TryParse(settings.HeroImageMediaId, out var heroId) ? (context.IsPreview ? $"/admin/media/{heroId:D}/file" : $"/media/{heroId:D}") : null;
        foreach (var language in languages.Languages)
        {
            if (catalog.PathFor(definition.Key, language.Code) is { } alternate)
            {
                seo.Alternates[language.Code] = alternate;
            }
        }

        var breadcrumbs = new List<(string Title, string Path)> { (localizer["Public.Nav.Home"].Value, $"/{lang}/") };
        foreach (var ancestor in catalog.Ancestors(definition))
        {
            breadcrumbs.Add((effective.Label(ancestor.Key, lang) ?? ancestor.Translations[lang].Nav, catalog.PathFor(ancestor.Key, lang)!));
        }

        breadcrumbs.Add((text.Nav, path));

        var related = definition.Related
            .Select(effective.Find)
            .Where(candidate => candidate is not null && candidate.Translations.ContainsKey(lang))
            .Select(candidate => (candidate!, candidate!.Translations[lang], catalog.PathFor(candidate.Key, lang)!))
            .ToList();

        // Service JSON-LD lists the offerings the page actually shows.
        var offerings = definition.Type == "service"
            ? text.Sections.Where(section => section.Kind == "offerings").SelectMany(section => section.Items).Select(item => item.Title).ToList()
            : [];
        seo.StructuredDataJson = StructuredDataBuilder.BuildSitePage(
            lang,
            site.Value.PublicBaseUrl,
            localizer["Site.Name"].Value,
            definition.Key == "about" ? "about" : definition.Type,
            text.H1,
            text.Description,
            path,
            breadcrumbs,
            offerings);

        ViewData["PageClass"] = "site-page";
        ViewData["Nav"] = definition.Key.Split('.')[0];
        // Explicit path: the admin preview renders this same view from the Admin area.
        return View("~/Views/SitePage/Show.cshtml", new SitePageViewModel(definition, text, lang, breadcrumbs, related));
    }

    private PageTextOverride? OverrideText(string key, string lang) => context.Snapshot.Pages.GetValueOrDefault(key)?.Translations.GetValueOrDefault(lang);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
