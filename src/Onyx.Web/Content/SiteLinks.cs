using Onyx.Web.Content.SitePages;
using Onyx.Web.Leads;
using Onyx.Web.Localization;
using NewsPages = Onyx.Web.News.NewsPages;
using SearchPages = Onyx.Web.Search.SearchPages;

namespace Onyx.Web.Content;

/// <summary>
/// The public links the shared header, footer and homepage may offer: only routes that exist (code routes, site pages and
/// section listings). <see cref="Page"/> resolves a site page by key. All paths are root-relative and percent-encoded.
/// </summary>
public sealed record SiteLinks(
    string Home,
    string Services,
    string About,
    string? Work,
    string? Insights,
    string? News,
    string? Contact,
    string? StartProject,
    string? BookConsultation,
    string? Search,
    SitePageCatalog Catalog,
    string LanguageCode)
{
    public static SiteLinks For(LocalizedRouteTable routes, SitePageCatalog catalog, string languageCode)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(catalog);
        var home = $"/{languageCode}/";
        return new SiteLinks(
            home,
            catalog.PathFor("services", languageCode) ?? home + "#services",
            catalog.PathFor("about", languageCode) ?? home + "#why-onyx",
            Section("work", languageCode),
            Section("insights", languageCode),
            routes.PathFor(NewsPages.Hub, languageCode),
            routes.PathFor(LeadPages.Contact, languageCode),
            routes.PathFor(LeadPages.StartProject, languageCode),
            routes.PathFor(LeadPages.BookConsultation, languageCode),
            routes.PathFor(SearchPages.Search, languageCode),
            catalog,
            languageCode);
    }

    /// <summary>Path of a site page in the current language, or null when the page does not exist.</summary>
    public string? Page(string key) => Catalog.PathFor(key, LanguageCode);

    /// <summary>Navigation label of a site page in the current language.</summary>
    public string? Label(string key) => Catalog.Find(key)?.Translations.GetValueOrDefault(LanguageCode)?.Nav;

    private static string? Section(string section, string languageCode) =>
        SectionRoutes.PathOf(section, languageCode) is { } path ? SitePaths.Encode(path) : null;
}
