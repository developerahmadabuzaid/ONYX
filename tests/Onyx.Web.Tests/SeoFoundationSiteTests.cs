using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Onyx.Contracts.Content;
using Onyx.Contracts.Organization;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

public sealed partial class SeoFoundationSiteTests : IAsyncLifetime
{
    private static readonly Guid ImageId = Guid.CreateVersion7();

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
    public async Task Trailing_slash_and_slashless_home_urls_redirect_to_the_single_canonical_form()
    {
        using var page = await _browser.GetPathAsync("/ar/الخدمات/حلول/?utm_source=x");
        using var home = await _browser.GetPathAsync("/en");

        Assert.Equal(HttpStatusCode.MovedPermanently, page.StatusCode);
        Assert.Equal("/ar/" + Uri.EscapeDataString("الخدمات") + "/" + Uri.EscapeDataString("حلول") + "?utm_source=x", page.Location());
        Assert.Empty(_factory.Api.RequestsTo("api/v1/content/resolve"));
        Assert.Equal(HttpStatusCode.MovedPermanently, home.StatusCode);
        Assert.Equal("/en/", home.Location());
    }

    [Fact]
    public async Task The_cookie_dependent_root_redirect_and_language_switch_are_never_cached()
    {
        using var root = await _browser.GetPathAsync("/");
        using var language = await _browser.GetPathAsync("/en/language");

        Assert.Contains("no-store", root.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Contains("private", root.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Contains("Cookie", root.Headers.Vary);
        Assert.Contains("no-store", language.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Language_switch_links_are_not_followed_by_crawlers()
    {
        var html = await _browser.GetHtmlAsync("/ar/");

        var switchLinks = SwitchLink().Matches(html);
        Assert.NotEmpty(switchLinks);
        Assert.All(switchLinks, link => Assert.Contains("rel=\"nofollow\"", link.Value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_home_page_carries_website_data_and_organization_data_only_when_verified()
    {
        var withoutOrganization = JsonLd(await _browser.GetHtmlAsync("/ar/"));
        Assert.Equal(["WebSite"], Types(withoutOrganization));
        Assert.Equal("Onyx Vanguard Solutions", withoutOrganization.RootElement.GetProperty("@graph")[0].GetProperty("name").GetString());

        var logo = Guid.CreateVersion7();
        _factory.Api.On(HttpMethod.Get, "api/v1/organization", _ => FakeOnyxApi.Json(new PublicOrganizationResponse("Onyx Vanguard Solutions", "https://www.onyx.test/", logo, 512, 512, null)));
        var withOrganization = JsonLd(await _browser.GetHtmlAsync("/en/"));

        Assert.Equal(["WebSite", "Organization"], Types(withOrganization));
        var organization = withOrganization.RootElement.GetProperty("@graph")[1];
        Assert.Equal($"https://www.onyx.test/media/{logo:D}", organization.GetProperty("logo").GetProperty("url").GetString());
        Assert.False(organization.TryGetProperty("address", out _));
        Assert.False(organization.TryGetProperty("email", out _));
        Assert.False(organization.TryGetProperty("sameAs", out _));
    }

    [Fact]
    public async Task The_featured_image_loads_eagerly_with_high_priority_and_body_images_lazily()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/content/resolve", _ => FakeOnyxApi.Json(new ContentResolveResponse("Content", null, Page())));

        var html = await _browser.GetHtmlAsync("/en/about");

        var images = ImageTag().Matches(html).Select(match => match.Value).ToList();
        Assert.Equal(2, images.Count);
        Assert.Contains("fetchpriority=\"high\"", images[0], StringComparison.Ordinal);
        Assert.DoesNotContain("loading=\"lazy\"", images[0], StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", images[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_indexnow_key_file_is_served_only_when_the_api_provides_a_key()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _browser.GetPathAsync("/indexnow-key.txt")).StatusCode);

        _factory.Api.On(HttpMethod.Get, "api/v1/search-engine-verification/indexnow-key", _ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("0123456789abcdef") });
        using var key = await _browser.GetPathAsync("/indexnow-key.txt");

        Assert.Equal("0123456789abcdef", await key.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("text/plain", key.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task After_launch_robots_lists_the_sitemap_and_blocks_admin_preview_and_language_switching()
    {
        await using var launched = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Site:AllowIndexing"] = "true" })));
        using var browser = launched.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var robots = await (await browser.GetPathAsync("/robots.txt")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Disallow: /admin\n", robots, StringComparison.Ordinal);
        Assert.Contains("Disallow: /ar/preview\n", robots, StringComparison.Ordinal);
        Assert.Contains("Disallow: /en/language\n", robots, StringComparison.Ordinal);
        Assert.Contains("Sitemap: https://www.onyx.test/sitemap.xml", robots, StringComparison.Ordinal);
        Assert.DoesNotContain("Disallow: /\n", robots, StringComparison.Ordinal);
    }

    private static JsonDocument JsonLd(string html) =>
        JsonDocument.Parse(Regex.Match(html, "<script type=\"application/ld\\+json\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value);

    private static List<string?> Types(JsonDocument document) =>
        document.RootElement.GetProperty("@graph").EnumerateArray().Select(node => node.GetProperty("@type").GetString()).ToList();

    private static PublicContentResponse Page()
    {
        var media = new PublicMediaResponse(ImageId, "a.png", "image/png", 10, 10, "Alt", null);
        return new PublicContentResponse(
            Guid.CreateVersion7(), "Page", "en", "About", "Summary",
            [new PublicBlockResponse("Image", null, null, null, null, media, null)],
            [],
            new PublicSeoResponse("About", null, "/en/about", true, true, new Dictionary<string, string> { ["en"] = "/en/about" }, null, null, null, null, ["WebPage"]),
            [], [], null, media, null, null);
    }

    [GeneratedRegex("<a href=\"/[a-z]{2}/language\\?[^>]*>")]
    private static partial Regex SwitchLink();

    [GeneratedRegex("<img [^>]*>")]
    private static partial Regex ImageTag();
}
