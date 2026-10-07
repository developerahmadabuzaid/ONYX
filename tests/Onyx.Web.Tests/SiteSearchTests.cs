using System.Net;
using Onyx.Contracts.Content;
using Onyx.Contracts.Search;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>The public search page: GET only, never indexed or cached, results from the API grouped by type.</summary>
public sealed class SiteSearchTests : IAsyncLifetime
{
    private const string SearchApi = "api/v1/search";

    private readonly OnyxWebFactory _factory = new();
    private HttpClient _browser = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        // Indexing is allowed site-wide (as after launch), so the noindex below is the search page's own.
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
    public async Task Every_public_page_has_a_get_search_form_in_its_language()
    {
        var english = await _browser.GetHtmlAsync("/en/contact");
        var arabic = await _browser.GetHtmlAsync("/ar/" + Uri.EscapeDataString("تواصل-معنا"));

        Assert.Contains("<form method=\"get\" action=\"/en/search\" role=\"search\">", english, StringComparison.Ordinal);
        Assert.Contains("type=\"search\" name=\"q\"", english, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"100\"", english, StringComparison.Ordinal);
        Assert.Contains("role=\"search\"", arabic, StringComparison.Ordinal);
        Assert.DoesNotContain("action=\"/en/search\"", arabic, StringComparison.Ordinal);
        Assert.Contains("بحث", ArabicActionOf(arabic), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_empty_search_page_is_noindex_no_store_and_calls_no_api()
    {
        using var response = await _browser.GetPathAsync("/en/search");
        var html = await response.Content.ReadAsStringAsync(Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("<meta name=\"robots\" content=\"noindex, follow\">", html, StringComparison.Ordinal);
        Assert.Empty(_factory.Api.RequestsTo(SearchApi));

        using var arabic = await _browser.GetPathAsync("/ar/" + Uri.EscapeDataString("بحث"));
        Assert.Equal(HttpStatusCode.OK, arabic.StatusCode);
        Assert.Contains("lang=\"ar\" dir=\"rtl\"", await arabic.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Results_are_grouped_with_encoded_links_and_the_query_is_capped_before_the_api()
    {
        _factory.Api.On(HttpMethod.Get, SearchApi, _ => FakeOnyxApi.Json(new SiteSearchResponse("automation", "ar", 3,
        [
            new SearchResultGroupResponse("Services", 2, [new SearchResultItemResponse("أتمتة تجريبية", "ملخص تجريبي.", "/ar/الخدمات/أتمتة-تجريبية", "ServicePillar")]),
            new SearchResultGroupResponse("Pages", 1, [new SearchResultItemResponse("<b>Sample</b>", string.Empty, "/ar/صفحة", "Page")]),
        ])));

        var html = await _browser.GetHtmlAsync("/ar/" + Uri.EscapeDataString("بحث") + "?q=" + new string('a', 150));

        var request = Assert.Single(_factory.Api.RequestsTo(SearchApi));
        Assert.Contains("language=ar", request.Query, StringComparison.Ordinal);
        Assert.Contains("q=" + new string('a', 100), request.Query, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('a', 101), request.Query, StringComparison.Ordinal);
        Assert.Contains("href=\"/ar/" + Uri.EscapeDataString("الخدمات") + "/" + Uri.EscapeDataString("أتمتة-تجريبية") + "\"", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;Sample&lt;/b&gt;", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("(2)", StringComparison.Ordinal) < html.IndexOf("(1)", StringComparison.Ordinal));
        Assert.Contains("<meta name=\"robots\" content=\"noindex, follow\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rate_limited_search_says_so_and_an_empty_result_says_nothing_was_found()
    {
        _factory.Api.On(HttpMethod.Get, SearchApi, _ => FakeOnyxApi.Problem(HttpStatusCode.TooManyRequests, "RateLimit.Exceeded"));
        var limited = await _browser.GetHtmlAsync("/en/search?q=automation");
        Assert.Contains("Too many attempts", limited, StringComparison.Ordinal);

        _factory.Api.On(HttpMethod.Get, SearchApi, _ => FakeOnyxApi.Json(new SiteSearchResponse("nothing", "en", 0, [])));
        var empty = await _browser.GetHtmlAsync("/en/search?q=nothing");
        Assert.Contains("role=\"status\"", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("Too many attempts", empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_search_segment_is_reserved_and_other_slugs_under_it_are_not_found()
    {
        Assert.Contains("بحث", ContentRouteSegments.Reserved);

        using var response = await _browser.GetPathAsync("/en/search/anything");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string ArabicActionOf(string html)
    {
        var start = html.IndexOf("role=\"search\"", StringComparison.Ordinal);
        var action = html.LastIndexOf("action=\"", start, StringComparison.Ordinal) + "action=\"".Length;
        return System.Net.WebUtility.HtmlDecode(Uri.UnescapeDataString(html[action..html.IndexOf('"', action)]));
    }
}
