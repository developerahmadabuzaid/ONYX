using System.Net;
using System.Xml.Linq;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.News;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>
/// The public News Hub and the admin news screens, against a fake API. Public pages always show attribution; items are
/// noindex unless marked indexable; unsafe original links are never rendered; admin changes need antiforgery tokens.
/// </summary>
public sealed class NewsSiteTests : IAsyncLifetime
{
    private static readonly DateTimeOffset SourceTime = new(2026, 10, 6, 8, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OnyxTime = new(2026, 10, 6, 11, 0, 0, TimeSpan.Zero);
    private static readonly Guid ItemId = Guid.CreateVersion7();

    private readonly OnyxWebFactory _factory = new();
    private HttpClient _browser = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        // Indexing allowed site-wide (as after launch), so robots decisions below are the News Hub's own.
        _factory.Settings["Site:AllowIndexing"] = "true";
        _browser = _factory.CreateBrowser();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _browser.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task The_hub_lists_items_with_attribution_and_is_indexable_only_when_it_has_items()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/news", _ => FakeOnyxApi.Json(Hub([Summary()])));

        using var response = await _browser.GetPathAsync("/en/news");
        var html = await response.Content.ReadAsStringAsync(Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Summary by Onyx — reported by Sample Publisher", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/en/news/sample-headline\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/en/news/cybersecurity\"", html, StringComparison.Ordinal);
        Assert.Contains("datetime=\"2026-10-06\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("noindex", html, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"canonical\" href=\"https://www.onyx.test/en/news\">", html, StringComparison.Ordinal);
        Assert.Contains("hreflang=\"ar\" href=\"https://www.onyx.test/ar/" + Uri.EscapeDataString("آخر-الأخبار") + "\"", html, StringComparison.Ordinal);
        Assert.Contains("language=en", Assert.Single(_factory.Api.RequestsTo("api/v1/news")).Query, StringComparison.Ordinal);

        _factory.Api.On(HttpMethod.Get, "api/v1/news", _ => FakeOnyxApi.Json(Hub([])));
        var empty = await _browser.GetHtmlAsync("/en/news");
        Assert.Contains("<meta name=\"robots\" content=\"noindex, follow\">", empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_arabic_item_is_rtl_noindex_by_default_and_shows_the_full_attribution()
    {
        var slug = "عنوان-تجريبي";
        _factory.Api.On(HttpMethod.Get, "api/v1/news/resolve", _ => FakeOnyxApi.Json(new NewsResolveResponse(null, Item("ar", slug, indexable: false, "https://publisher-sample.com/story?id=1&ref=a"))));

        var html = await _browser.GetHtmlAsync("/ar/" + Uri.EscapeDataString("آخر-الأخبار") + "/" + Uri.EscapeDataString(slug));

        Assert.Contains("lang=\"ar\" dir=\"rtl\"", html, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"robots\" content=\"noindex, follow\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<link rel=\"alternate\" hreflang=", html, StringComparison.Ordinal);
        Assert.Contains("ملخص من Onyx — نقلاً عن Sample Publisher", html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://publisher-sample.com/story?id=1&amp;ref=a\" rel=\"nofollow noopener noreferrer external\"", html, StringComparison.Ordinal);
        Assert.Contains("datetime=\"2026-10-06T08:30:00Z\"", html, StringComparison.Ordinal);
        Assert.Contains("datetime=\"2026-10-06T11:00:00Z\"", html, StringComparison.Ordinal);
        Assert.Contains("slug=" + Uri.EscapeDataString(slug), Assert.Single(_factory.Api.RequestsTo("api/v1/news/resolve")).Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_indexable_item_declares_alternates_and_unsafe_original_links_are_never_rendered()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/news/resolve", _ => FakeOnyxApi.Json(new NewsResolveResponse(null, Item("en", "sample-headline", indexable: true, "javascript:alert(1)"))));

        var html = await _browser.GetHtmlAsync("/en/news/sample-headline");

        Assert.DoesNotContain("noindex", html, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"alternate\" hreflang=\"ar\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", html, StringComparison.Ordinal);
        Assert.Contains("Summary by Onyx — reported by Sample Publisher", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_and_nested_slugs_are_not_found()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/news/resolve", _ => FakeOnyxApi.Json(new NewsResolveResponse(null, null)));

        Assert.Equal(HttpStatusCode.NotFound, (await _browser.GetPathAsync("/en/news/no-such-item")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _browser.GetPathAsync("/en/news/a/b")).StatusCode);
        Assert.Contains("news", ContentRouteSegments.Reserved);
        Assert.Contains("آخر-الأخبار", ContentRouteSegments.Reserved);
    }

    [Fact]
    public async Task The_homepage_shows_latest_news_only_when_there_is_some()
    {
        var without = await _browser.GetHtmlAsync("/en/");
        Assert.DoesNotContain("latest-news-heading", without, StringComparison.Ordinal);

        _factory.Api.On(HttpMethod.Get, "api/v1/news/latest", _ => FakeOnyxApi.Json(new List<PublicNewsItemSummaryResponse> { Summary() }));
        var with = await _browser.GetHtmlAsync("/en/");
        Assert.Contains("latest-news-heading", with, StringComparison.Ordinal);
        Assert.Contains("Summary by Onyx — reported by Sample Publisher", with, StringComparison.Ordinal);
        Assert.Contains("href=\"/en/news\"", with, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_language_sitemap_includes_news_entries_from_the_api()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/content/sitemap", _ => FakeOnyxApi.Json(new List<SitemapEntryResponse>()));
        _factory.Api.On(HttpMethod.Get, "api/v1/news/sitemap", _ => FakeOnyxApi.Json(new List<SitemapEntryResponse>
        {
            new("/en/news", OnyxTime, new Dictionary<string, string> { ["en"] = "/en/news", ["ar"] = "/ar/آخر-الأخبار" }),
        }));

        var xml = XDocument.Parse(await _browser.GetHtmlAsync("/sitemaps/en.xml"));

        Assert.Contains("https://www.onyx.test/en/news", xml.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value));
    }

    [Fact]
    public async Task Admin_news_menus_follow_permissions()
    {
        await SignInAsync(Permissions.News.View);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/notifications", _ => FakeOnyxApi.Json(new PagedResult<Onyx.Contracts.Notifications.AdminNotificationResponse>([], 1, 20, 0)));

        var html = await _browser.GetHtmlAsync("/admin/notifications");

        Assert.Contains("href=\"/admin/news\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/news/categories\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/news/sources\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_item_actions_send_the_version_and_need_an_antiforgery_token()
    {
        await SignInAsync(Permissions.News.View, Permissions.News.Edit, Permissions.News.Publish);
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/news/{ItemId:D}", _ => FakeOnyxApi.Json(AdminItem()));
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/news-categories", _ => FakeOnyxApi.Json(new List<NewsCategoryResponse>()));
        _factory.Api.On(HttpMethod.Post, $"api/v1/admin/news/{ItemId:D}/publish", _ => FakeOnyxApi.Json(AdminItem()));

        var page = await _browser.GetHtmlAsync($"/admin/news/{ItemId:D}");
        Assert.Contains("Sample Publisher", page, StringComparison.Ordinal);
        Assert.Contains("rel=\"nofollow noopener noreferrer\"", page, StringComparison.Ordinal);
        Assert.Contains("no Onyx summary yet", page, StringComparison.Ordinal);

        using var publish = await _browser.PostFormAsync($"/admin/news/{ItemId:D}/state/publish", new Dictionary<string, string> { ["version"] = "7" }, BrowserExtensions.AntiforgeryToken(page));
        Assert.Equal(HttpStatusCode.Redirect, publish.StatusCode);
        Assert.Contains("\"version\":7", Assert.Single(_factory.Api.RequestsTo($"api/v1/admin/news/{ItemId:D}/publish")).Body, StringComparison.Ordinal);

        using var unknown = await _browser.PostFormAsync($"/admin/news/{ItemId:D}/state/approve-everything", new Dictionary<string, string> { ["version"] = "7" }, BrowserExtensions.AntiforgeryToken(page));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        using var forged = await _browser.PostFormAsync($"/admin/news/{ItemId:D}/state/delete", new Dictionary<string, string> { ["version"] = "7" }, null);
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Empty(_factory.Api.RequestsTo($"api/v1/admin/news/{ItemId:D}/delete"));
    }

    private async Task SignInAsync(params string[] permissions)
    {
        AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", permissions));
        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    private static PublicNewsItemSummaryResponse Summary() =>
        new("Sample headline", "A sample Onyx summary for tests.", "/en/news/sample-headline", "Sample Publisher", "Cybersecurity", SourceTime, OnyxTime);

    private static PublicNewsListResponse Hub(IReadOnlyList<PublicNewsItemSummaryResponse> items) =>
        new("en", null, items, 1, 20, items.Count,
            [new PublicNewsCategoryResponse("cybersecurity", "Cybersecurity", "cybersecurity", "/en/news/cybersecurity")],
            new Dictionary<string, string> { ["ar"] = "/ar/آخر-الأخبار", ["en"] = "/en/news" });

    private static PublicNewsItemResponse Item(string language, string slug, bool indexable, string originalUrl)
    {
        var hub = language == "ar" ? "/ar/آخر-الأخبار" : "/en/news";
        var alternates = indexable
            ? new Dictionary<string, string> { ["ar"] = "/ar/آخر-الأخبار/عنوان-تجريبي", ["en"] = "/en/news/sample-headline" }
            : new Dictionary<string, string>();
        return new PublicNewsItemResponse(
            language == "ar" ? "عنوان تجريبي" : "Sample headline", "A sample Onyx summary for tests.", $"{hub}/{slug}", "Sample Publisher", originalUrl,
            SourceTime, OnyxTime, new PublicNewsCategoryResponse("cybersecurity", "Cybersecurity", "cybersecurity", $"{hub}/cybersecurity"), indexable, alternates);
    }

    private static NewsItemResponse AdminItem() =>
        new(ItemId, Guid.CreateVersion7(), "Sample Publisher", "https://publisher-sample.com/story", "Original sample title", "en", SourceTime, OnyxTime,
            Guid.CreateVersion7(), null, "Held", "SummaryUnavailable", null, null, false, false, null, null, [], 7);
}
