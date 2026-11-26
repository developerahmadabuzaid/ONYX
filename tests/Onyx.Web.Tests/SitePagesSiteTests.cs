using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Onyx.Contracts.Content;
using Onyx.Web.Content.SitePages;
using Onyx.Web.Localization;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

public sealed partial class SitePagesSiteTests : IAsyncLifetime
{
    private static readonly SitePageCatalog Catalog = SitePageCatalog.LoadEmbedded();

    private readonly OnyxWebFactory _factory = new();
    private HttpClient _browser = null!;

    public ValueTask InitializeAsync()
    {
        _browser = _factory.CreateBrowser();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _browser.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Every_site_page_renders_with_complete_seo_in_both_languages()
    {
        foreach (var page in Catalog.Pages)
        {
            foreach (var language in new[] { "ar", "en" })
            {
                var path = Catalog.PathFor(page.Key, language)!;
                var html = await _browser.GetHtmlAsync(path);

                Assert.Equal(1, H1().Count(html));
                Assert.Contains($"<link rel=\"canonical\" href=\"{OnyxWebFactory.PublicBaseUrl}{path}\">", html, StringComparison.Ordinal);
                Assert.Contains($"hreflang=\"ar\" href=\"{OnyxWebFactory.PublicBaseUrl}{Catalog.PathFor(page.Key, "ar")}\"", html, StringComparison.Ordinal);
                Assert.Contains($"hreflang=\"en\" href=\"{OnyxWebFactory.PublicBaseUrl}{Catalog.PathFor(page.Key, "en")}\"", html, StringComparison.Ordinal);
                Assert.Contains($"hreflang=\"x-default\" href=\"{OnyxWebFactory.PublicBaseUrl}{Catalog.PathFor(page.Key, "ar")}\"", html, StringComparison.Ordinal);
                Assert.Contains("<meta name=\"description\"", html, StringComparison.Ordinal);
                Assert.Contains("<meta property=\"og:title\"", html, StringComparison.Ordinal);
                Assert.Contains("\"BreadcrumbList\"", html, StringComparison.Ordinal);
                Assert.Contains($"<html lang=\"{language}\" dir=\"{(language == "ar" ? "rtl" : "ltr")}\">", html, StringComparison.Ordinal);
                if (page.Type == "service")
                {
                    Assert.Contains("\"@type\":\"Service\"", html, StringComparison.Ordinal);
                }

                if (page.Draft)
                {
                    Assert.Contains("callout--draft", html, StringComparison.Ordinal);
                }
            }
        }
    }

    [Fact]
    public async Task Site_pages_are_served_without_calling_the_api()
    {
        await _browser.GetHtmlAsync(Catalog.PathFor("services.cloud", "en")!);

        // The page itself is code-owned; the only call is the cached load of published Admin overrides (shared by every page).
        Assert.All(_factory.Api.Requests, request => Assert.Equal("api/v1/site-content", request.Path));
    }

    [Fact]
    public async Task The_sitemap_lists_indexable_site_pages_with_alternates_but_never_drafts()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/content/sitemap", _ => FakeOnyxApi.Json(new List<SitemapEntryResponse>()));
        _factory.Api.On(HttpMethod.Get, "api/v1/news/sitemap", _ => FakeOnyxApi.Json(new List<SitemapEntryResponse>()));

        var xml = XDocument.Parse(await _browser.GetHtmlAsync("/sitemaps/en.xml"));
        var locations = xml.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value).ToList();
        var alternates = xml.Descendants().Where(e => e.Name.LocalName == "link").Select(e => (string?)e.Attribute("href")).ToList();

        foreach (var page in Catalog.Pages)
        {
            var url = OnyxWebFactory.PublicBaseUrl + Catalog.PathFor(page.Key, "en");
            if (page.Draft)
            {
                Assert.DoesNotContain(url, locations);
            }
            else
            {
                Assert.Contains(url, locations);
                Assert.Contains(OnyxWebFactory.PublicBaseUrl + Catalog.PathFor(page.Key, "ar"), alternates);
            }
        }
    }

    [Fact]
    public async Task Every_internal_link_on_site_pages_and_the_homepage_resolves()
    {
        var checkedLinks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in new[] { "/en/", "/ar/", Catalog.PathFor("services", "en")!, Catalog.PathFor("services.ai", "ar")!, Catalog.PathFor("about", "en")! })
        {
            var html = await _browser.GetHtmlAsync(start);
            foreach (Match match in InternalLink().Matches(html))
            {
                var href = WebUtilityDecode(match.Groups["href"].Value).Split('#')[0];
                var firstSegment = Uri.UnescapeDataString(href.Split('?')[0].Trim('/').Split('/').ElementAtOrDefault(1) ?? string.Empty);

                // Section listings and the News Hub are API-backed and covered by their own tests.
                if (href.Length == 0 || href.Contains("/language?", StringComparison.Ordinal)
                    || firstSegment is "work" or "insights" or "news" or "أعمالنا" or "المعرفة" or "آخر-الأخبار"
                    || !checkedLinks.Add(href))
                {
                    continue;
                }

                using var response = await _browser.GetPathAsync(href);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{href} (linked from {start}) returned {(int)response.StatusCode}");
            }
        }

        Assert.NotEmpty(checkedLinks);
    }

    [Fact]
    public async Task Service_pages_preselect_their_pillar_in_the_project_brief()
    {
        var html = await _browser.GetHtmlAsync(Catalog.PathFor("services.ai", "en")!);
        Assert.Contains("?pillar=ai\"", html, StringComparison.Ordinal);

        var brief = await _browser.GetHtmlAsync("/en/start-a-project?pillar=ai");
        Assert.Matches("value=\"ai\" checked=\"checked\"", brief);

        var ignored = await _browser.GetHtmlAsync("/en/start-a-project?pillar=unknown");
        Assert.DoesNotMatch("checked=\"checked\"", ignored);
    }

    [Fact]
    public async Task Unknown_paths_under_services_still_reach_the_cms()
    {
        await _browser.GetPathAsync("/en/services/not-a-site-page");

        Assert.NotEmpty(_factory.Api.RequestsTo("api/v1/content/resolve"));
    }

    private static string WebUtilityDecode(string value) => WebUtility.HtmlDecode(value);

    [GeneratedRegex("<h1[\\s>]")]
    private static partial Regex H1();

    [GeneratedRegex("href=\"(?<href>/(ar|en)/[^\"]*)\"")]
    private static partial Regex InternalLink();
}
