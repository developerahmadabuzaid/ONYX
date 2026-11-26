using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Onyx.Contracts.Content;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

public sealed class ContentSiteTests : IAsyncLifetime
{
    private static readonly Guid PageId = Guid.CreateVersion7();
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
    public async Task Live_content_renders_encoded_canonical_hreflang_and_structured_data()
    {
        ResolveReturns(request => request.Query.Contains("language=ar", StringComparison.Ordinal)
            ? new ContentResolveResponse("Content", null, Page("ar"))
            : new ContentResolveResponse("NotFound", null, null));

        using var response = await _browser.GetPathAsync("/ar/الخدمات/حلول-الذكاء-الاصطناعي");
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resolve = Assert.Single(_factory.Api.RequestsTo("api/v1/content/resolve"));
        Assert.Contains("path=" + Uri.EscapeDataString("الخدمات/حلول-الذكاء-الاصطناعي"), resolve.Query, StringComparison.Ordinal);
        var encoded = "/ar/" + Uri.EscapeDataString("الخدمات") + "/" + Uri.EscapeDataString("حلول-الذكاء-الاصطناعي");
        Assert.Contains($"<link rel=\"canonical\" href=\"{OnyxWebFactory.PublicBaseUrl}{encoded}\">", html, StringComparison.Ordinal);
        Assert.Contains($"hreflang=\"en\" href=\"{OnyxWebFactory.PublicBaseUrl}/en/services/ai-solutions\"", html, StringComparison.Ordinal);
        Assert.Contains($"hreflang=\"x-default\" href=\"{OnyxWebFactory.PublicBaseUrl}{encoded}\"", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"page-title\">حلول الذكاء الاصطناعي</h1>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"robots\" content=\"noindex, nofollow\">", html, StringComparison.Ordinal);
        var jsonLd = System.Text.RegularExpressions.Regex.Match(html, "<script type=\"application/ld\\+json\">(.*?)</script>", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(jsonLd.Success);
        Assert.DoesNotContain("</script", jsonLd.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(jsonLd.Groups[1].Value);
        var types = document.RootElement.GetProperty("@graph").EnumerateArray().Select(node => node.GetProperty("@type").GetString()).ToList();
        Assert.Equal(["WebPage", "Service"], types);
        Assert.DoesNotContain("aggregateRating", jsonLd.Groups[1].Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Former_urls_redirect_permanently_to_the_encoded_current_path()
    {
        ResolveReturns(_ => new ContentResolveResponse("Redirect", "/ar/الخدمات/جديد", null));

        using var response = await _browser.GetPathAsync("/ar/الخدمات/قديم");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/ar/" + Uri.EscapeDataString("الخدمات") + "/" + Uri.EscapeDataString("جديد"), response.Location());
    }

    [Theory]
    [InlineData("//evil.example/x")]
    [InlineData("https://evil.example/x")]
    public async Task Redirect_targets_can_never_leave_the_site(string target)
    {
        ResolveReturns(_ => new ContentResolveResponse("Redirect", target, null));

        using var response = await _browser.GetPathAsync("/en/old");

        Assert.NotEqual(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Null(response.Location());
    }

    [Theory]
    [InlineData("Gone", HttpStatusCode.Gone)]
    [InlineData("NotFound", HttpStatusCode.NotFound)]
    public async Task Offline_and_unknown_content_answer_410_and_404(string kind, HttpStatusCode expected)
    {
        ResolveReturns(_ => new ContentResolveResponse(kind, null, null));

        using var response = await _browser.GetPathAsync("/en/whatever");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Previews_are_never_indexed_cached_or_referred_and_load_images_with_the_token()
    {
        var token = new string('a', 43);
        _factory.Api.On(HttpMethod.Get, "api/v1/content/preview", request => request.Query.Contains($"token={token}", StringComparison.Ordinal)
            ? FakeOnyxApi.Json(new ContentPreviewResponse(Page("en"), SeoPreview(), _factory.Time.GetUtcNow().AddMinutes(30)))
            : FakeOnyxApi.Problem(HttpStatusCode.NotFound, "Preview.Invalid"));

        using var response = await _browser.GetPathAsync($"/en/preview?token={token}");
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("noindex, nofollow, noarchive", response.Headers.GetValues("X-Robots-Tag").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Contains("<meta name=\"robots\" content=\"noindex, nofollow\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("rel=\"canonical\"", html, StringComparison.Ordinal);
        Assert.Contains($"/media/{ImageId:D}?preview={token}", html, StringComparison.Ordinal);

        using var wrongLanguage = await _browser.GetPathAsync($"/ar/preview?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, wrongLanguage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _browser.GetPathAsync("/en/preview?token=expired")).StatusCode);
    }

    [Fact]
    public async Task Sitemaps_list_live_pages_with_absolute_encoded_urls_and_alternates()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/content/sitemap", _ => FakeOnyxApi.Json(new List<SitemapEntryResponse>
        {
            new("/ar/الخدمات/حلول", _factory.Time.GetUtcNow(), new Dictionary<string, string> { ["ar"] = "/ar/الخدمات/حلول", ["en"] = "/en/services/solutions" }),
        }));

        // Stage 11: the language sitemap also asks the API for News Hub entries (none here).
        _factory.Api.On(HttpMethod.Get, "api/v1/news/sitemap", _ => FakeOnyxApi.Json(new List<SitemapEntryResponse>()));

        using var index = await _browser.GetPathAsync("/sitemap.xml");
        var indexXml = XDocument.Parse(await index.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["https://www.onyx.test/sitemaps/ar.xml", "https://www.onyx.test/sitemaps/en.xml"], indexXml.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value));
        Assert.Equal("noindex, nofollow", index.Headers.GetValues("X-Robots-Tag").Single());

        using var arabic = await _browser.GetPathAsync("/sitemaps/ar.xml");
        var xml = XDocument.Parse(await arabic.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var locations = xml.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value).ToList();
        Assert.Contains("https://www.onyx.test/ar/", locations);
        Assert.Contains("https://www.onyx.test/ar/" + Uri.EscapeDataString("الخدمات") + "/" + Uri.EscapeDataString("حلول"), locations);
        var alternates = xml.Descendants().Where(e => e.Name.LocalName == "link").Select(e => (string)e.Attribute("hreflang")!).ToList();
        Assert.Contains("x-default", alternates);
    }

    [Fact]
    public async Task Robots_blocks_everything_before_launch()
    {
        using var response = await _browser.GetPathAsync("/robots.txt");
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal("User-agent: *\nDisallow: /\n", text);
    }

    [Fact]
    public async Task Media_is_proxied_only_for_images_and_never_cached_in_previews()
    {
        _factory.Api.On(HttpMethod.Get, $"api/v1/media/{ImageId:D}", request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]) { Headers = { ContentType = new("image/png") } },
        });
        var htmlId = Guid.CreateVersion7();
        _factory.Api.On(HttpMethod.Get, $"api/v1/media/{htmlId:D}", _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<script>alert(1)</script>", System.Text.Encoding.UTF8, "text/html"),
        });

        using var image = await _browser.GetPathAsync($"/media/{ImageId:D}");
        using var preview = await _browser.GetPathAsync($"/media/{ImageId:D}?preview={new string('b', 43)}");
        using var html = await _browser.GetPathAsync($"/media/{htmlId:D}");

        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", preview.Headers.CacheControl?.ToString());
        Assert.Contains("previewToken=", _factory.Api.RequestsTo($"api/v1/media/{ImageId:D}").Last().Query, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, html.StatusCode);
    }

    private void ResolveReturns(Func<RecordedApiRequest, ContentResolveResponse> respond) =>
        _factory.Api.On(HttpMethod.Get, "api/v1/content/resolve", request => FakeOnyxApi.Json(respond(request)));

    private static PublicContentResponse Page(string language)
    {
        var arPath = "/ar/الخدمات/حلول-الذكاء-الاصطناعي";
        var enPath = "/en/services/ai-solutions";
        var title = language == "ar" ? "حلول الذكاء الاصطناعي" : "AI solutions";
        return new PublicContentResponse(
            PageId,
            "Service",
            language,
            title,
            "<script>alert(1)</script>",
            [
                new PublicBlockResponse("Heading", 2, [new PublicSpanResponse("Section", null, null, false)], null, null, null, null),
                new PublicBlockResponse("Image", null, null, null, null, new PublicMediaResponse(ImageId, "a.png", "image/png", 10, 10, "Alt", null), null),
            ],
            [],
            new PublicSeoResponse(
                title, "Description", language == "ar" ? arPath : enPath, true, true,
                new Dictionary<string, string> { ["ar"] = arPath, ["en"] = enPath }, arPath, null, null, null, ["WebPage", "Service"]),
            [new BreadcrumbResponse(title, language == "ar" ? arPath : enPath)],
            [],
            null,
            null,
            null,
            null);
    }

    private static SeoPreviewResponse SeoPreview() =>
        new("/en/services/ai-solutions", "AI solutions", null, "index, follow", null, [new HeadingOutlineResponse(1, "AI solutions")], [], 0, ["WebPage"],
            new QualityReportResponse(true, []));
}
