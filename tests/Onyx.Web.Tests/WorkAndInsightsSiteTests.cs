using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Proof;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

public sealed class WorkAndInsightsSiteTests : IAsyncLifetime
{
    private static readonly Guid ItemId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Published = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

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
    public async Task Section_roots_render_live_listings_in_both_languages()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/content/listing", request => FakeOnyxApi.Json(new PagedResult<ListingItemResponse>(
            [new ListingItemResponse(ItemId, "Project", request.Query.Contains("language=ar", StringComparison.Ordinal) ? "مشروع" : "Sample project", null,
                request.Query.Contains("language=ar", StringComparison.Ordinal) ? "/ar/أعمالنا/مشروع" : "/en/work/sample-project", Published, null)],
            1, 20, 1)));

        var english = await _browser.GetHtmlAsync("/en/work");
        var arabic = await _browser.GetHtmlAsync("/ar/أعمالنا");

        Assert.Contains("section=work", _factory.Api.RequestsTo("api/v1/content/listing")[0].Query, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"page-title\">Our work</h1>", english, StringComparison.Ordinal);
        Assert.Contains("href=\"/en/work/sample-project\"", english, StringComparison.Ordinal);
        Assert.Contains($"hreflang=\"ar\" href=\"{OnyxWebFactory.PublicBaseUrl}/ar/{Uri.EscapeDataString("أعمالنا")}\"", english, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"page-title\">أعمالنا</h1>", arabic, StringComparison.Ordinal);
        Assert.Empty(_factory.Api.RequestsTo("api/v1/content/resolve"));
    }

    [Fact]
    public async Task An_empty_section_is_not_indexed_and_nested_sections_are_recognized()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/content/listing", _ => FakeOnyxApi.Json(new PagedResult<ListingItemResponse>([], 1, 20, 0)));

        using var launched = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            Microsoft.Extensions.Configuration.MemoryConfigurationBuilderExtensions.AddInMemoryCollection(configuration, new Dictionary<string, string?> { ["Site:AllowIndexing"] = "true" })));
        using var browser = launched.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var html = await (await browser.GetPathAsync("/en/work/case-studies")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("section=case-studies", _factory.Api.RequestsTo("api/v1/content/listing").Last().Query, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"robots\" content=\"noindex, follow\">", html, StringComparison.Ordinal);
        Assert.Contains("Nothing has been published here yet.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Project_pages_show_only_the_verified_proof_the_api_returns()
    {
        Resolve(Page("Project", "/en/work/sample-project", proof: new PublicProofResponse(
            [new PublicProofItemResponse("Online booking", "Patients book visits.", null, null, null, null, null)],
            [new PublicProofItemResponse("Faster reports", null, "35%", new DateOnly(2026, 9, 1), null, null, null)],
            [new PublicProofItemResponse("Clear and reliable <b>work</b>.", null, null, null, "Sample Person", "Manager", "Sample Org")])));

        var html = await _browser.GetHtmlAsync("/en/work/sample-project");

        Assert.Contains("Online booking", html, StringComparison.Ordinal);
        Assert.Contains("35%", html, StringComparison.Ordinal);
        Assert.Contains("datetime=\"2026-09-01\"", html, StringComparison.Ordinal);
        Assert.Contains("Sample Person, Manager, Sample Org", html, StringComparison.Ordinal);
        Assert.Contains("Clear and reliable &lt;b&gt;work&lt;/b&gt;.", html, StringComparison.Ordinal);

        Resolve(Page("Project", "/en/work/sample-project", proof: new PublicProofResponse([], [], [])));
        var empty = await _browser.GetHtmlAsync("/en/work/sample-project");
        Assert.DoesNotContain("Capabilities", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("What the client says", empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Insights_show_the_byline_dates_ai_disclosure_and_article_data()
    {
        Resolve(Page("Insight", "/en/insights/sample", insight: new PublicInsightResponse("Article", "AiAssisted", new PublicAuthorResponse("Sample Author", "Engineer")), structured: ["WebPage", "Article"]));

        var html = await _browser.GetHtmlAsync("/en/insights/sample");

        Assert.Contains("By Sample Author", html, StringComparison.Ordinal);
        Assert.Contains("datetime=\"2026-09-01\"", html, StringComparison.Ordinal);
        Assert.Contains("Written with AI assistance", html, StringComparison.Ordinal);
        using var jsonLd = JsonDocument.Parse(Regex.Match(html, "<script type=\"application/ld\\+json\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value);
        var article = jsonLd.RootElement.GetProperty("@graph").EnumerateArray().Single(node => node.GetProperty("@type").GetString() == "Article");
        Assert.Equal("Person", article.GetProperty("author").GetProperty("@type").GetString());
        Assert.Equal("Sample Author", article.GetProperty("author").GetProperty("name").GetString());
        Assert.Equal("2026-09-01T08:00:00Z", article.GetProperty("datePublished").GetString());

        Resolve(Page("Insight", "/en/insights/sample", insight: new PublicInsightResponse("Guide", "Human", null), structured: ["WebPage", "Article"]));
        var organization = await _browser.GetHtmlAsync("/en/insights/sample");
        Assert.Contains("By Onyx Vanguard Solutions", organization, StringComparison.Ordinal);
        Assert.DoesNotContain("Written with AI assistance", organization, StringComparison.Ordinal);
        Assert.Contains("\"author\":{\"@type\":\"Organization\"", organization, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Topic_pages_list_their_insights()
    {
        Resolve(Page("Topic", "/en/insights/topics/automation"));
        _factory.Api.On(HttpMethod.Get, "api/v1/content/listing", _ => FakeOnyxApi.Json(new PagedResult<ListingItemResponse>(
            [new ListingItemResponse(Guid.CreateVersion7(), "Insight", "Tagged insight", null, "/en/insights/tagged", Published, null)], 1, 20, 1)));

        var html = await _browser.GetHtmlAsync("/en/insights/topics/automation");

        Assert.Contains($"topic={ItemId:D}", _factory.Api.RequestsTo("api/v1/content/listing").Single().Query, StringComparison.Ordinal);
        Assert.Contains("href=\"/en/insights/tagged\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_homepage_shows_featured_work_only_when_there_is_some()
    {
        Assert.DoesNotContain("Featured work", await _browser.GetHtmlAsync("/en/"), StringComparison.Ordinal);

        _factory.Api.On(HttpMethod.Get, "api/v1/content/featured-projects", _ => FakeOnyxApi.Json(new List<ListingItemResponse>
        {
            new(ItemId, "Project", "Sample project", "Summary", "/en/work/sample-project", Published, null),
        }));
        var html = await _browser.GetHtmlAsync("/en/");

        Assert.Contains("Featured work", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/en/work/sample-project\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_homepage_shows_the_latest_insights_only_when_there_are_some()
    {
        Assert.DoesNotContain("insights-title", await _browser.GetHtmlAsync("/en/"), StringComparison.Ordinal);

        _factory.Api.On(HttpMethod.Get, "api/v1/content/listing", _ => FakeOnyxApi.Json(new PagedResult<ListingItemResponse>(
            [new ListingItemResponse(ItemId, "Insight", "Sample insight", "Summary", "/en/insights/sample-insight", Published, null)], 1, 20, 1)));
        var html = await _browser.GetHtmlAsync("/en/");

        Assert.Contains("insights-title", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/en/insights/sample-insight\"", html, StringComparison.Ordinal);
        Assert.Contains("section=insights", _factory.Api.RequestsTo("api/v1/content/listing").Last().Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_homepage_renders_without_insights_when_the_api_is_unreachable()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/content/listing", _ => throw new HttpRequestException("unreachable"));

        using var response = await _browser.GetAsync(new Uri("/en/", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("insights-title", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_proof_verification_and_proof_items_go_to_the_api_with_the_server_side_token()
    {
        var tokens = AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", Permissions.Content.View, Permissions.Proof.Verify, Permissions.Projects.View));
        using var login = await AdminApiStubs.LogInAsync(_browser);
        var detail = Detail();
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/content/{ItemId:D}", _ => FakeOnyxApi.Json(detail));
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/content/{ItemId:D}/quality", _ => FakeOnyxApi.Json(new QualityReportResponse(false, [])));
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/content/{ItemId:D}/publishing-time", _ => FakeOnyxApi.Json(new PublishingTimeRecommendationResponse("InsufficientData", null, "x")));
        _factory.Api.On(HttpMethod.Post, $"api/v1/admin/content/{ItemId:D}/proof-verification", _ => FakeOnyxApi.Json(detail));
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/proof-items", _ => FakeOnyxApi.Json(new List<ProofItemResponse>()));
        _factory.Api.On(HttpMethod.Post, "api/v1/admin/proof-items", _ => FakeOnyxApi.Json(new ProofItemResponse(
            Guid.CreateVersion7(), ItemId, "Metric", [], "35%", new DateOnly(2026, 9, 1), null, null, null, 0, "Draft", null, null, 1)));

        var overview = await _browser.GetHtmlAsync($"/admin/content/{ItemId:D}");
        Assert.Contains("Proof verification", overview, StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.AccessToken, overview, StringComparison.Ordinal);

        using var verify = await _browser.SubmitFormAsync($"/admin/content/{ItemId:D}", $"/admin/content/{ItemId:D}/proof-verification", new Dictionary<string, string>
        {
            ["sourceNote"] = "Owner confirmation",
            ["clientPermission"] = "NotApplicable",
            ["version"] = "4",
        });
        var sent = Assert.Single(_factory.Api.Requests, request => request.Path.EndsWith("/proof-verification", StringComparison.Ordinal));
        Assert.Equal(tokens.AccessToken, sent.BearerToken);
        Assert.Equal("Owner confirmation", sent.BodyAs<VerifyProofRequest>().SourceNote);

        using var create = await _browser.SubmitFormAsync($"/admin/content/{ItemId:D}/proof", $"/admin/content/{ItemId:D}/proof", new Dictionary<string, string>
        {
            ["Kind"] = "Metric",
            ["TitleEn"] = "Faster reports",
            ["TitleAr"] = "تقارير أسرع",
            ["Value"] = "35%",
            ["AsOf"] = "2026-09-01",
            ["SortOrder"] = "0",
        });
        var proof = Assert.Single(_factory.Api.Requests, request => request.Method == HttpMethod.Post && request.Path == "api/v1/admin/proof-items").BodyAs<SaveProofItemRequest>();
        Assert.Equal("Metric", proof.Kind);
        Assert.Equal(new DateOnly(2026, 9, 1), proof.AsOf);
        Assert.Equal(["ar", "en"], proof.Texts.Select(text => text.LanguageCode));
    }

    private void Resolve(PublicContentResponse content) =>
        _factory.Api.On(HttpMethod.Get, "api/v1/content/resolve", _ => FakeOnyxApi.Json(new ContentResolveResponse("Content", null, content)));

    private static PublicContentResponse Page(
        string type, string path, PublicProofResponse? proof = null, PublicInsightResponse? insight = null, IReadOnlyList<string>? structured = null) =>
        new(ItemId, type, "en", "Sample title", "Summary", [], [],
            new PublicSeoResponse("Sample title", null, path, true, true, new Dictionary<string, string> { ["en"] = path }, null, null, null, null, structured ?? ["WebPage"]),
            [], [], null, null, Published, Published, insight, proof);

    private static ContentDetailResponse Detail() =>
        new(ItemId, "Project", null, null, "Page", 0, "Draft", false, false, 2, null, null, null, null, null, null, null, null, null, null, "None", null,
            [new ContentTranslationResponse("en", "Sample project", "Summary", "sample-project", [], [], new SeoProfileDto(null, null, null, null, null, null, true, true, null, null, null, null, null, null, null), true)],
            [], 4);
}
