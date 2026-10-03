using System.Net;
using System.Net.Http.Headers;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

public sealed class PublicSiteTests : IAsyncLifetime
{
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
    public async Task Root_redirects_to_Arabic_by_default()
    {
        using var response = await _browser.GetPathAsync("/");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/ar/", response.Location());
    }

    [Fact]
    public async Task Root_ignores_Accept_Language()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/", UriKind.Relative));
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));

        using var response = await _browser.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("/ar/", response.Location());
    }

    [Fact]
    public async Task Root_honours_an_explicit_language_choice()
    {
        using var choice = await _browser.GetPathAsync("/en/language");
        var cookie = Assert.Single(choice.SetCookies("onyx.lang"));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);

        using var response = await _browser.GetPathAsync("/");

        Assert.Equal("/en/", response.Location());
    }

    [Theory]
    [InlineData("ar", "rtl")]
    [InlineData("en", "ltr")]
    public async Task Home_sets_language_direction_and_alternates(string language, string direction)
    {
        var html = await _browser.GetHtmlAsync($"/{language}/");

        Assert.Contains($"<html lang=\"{language}\" dir=\"{direction}\">", html, StringComparison.Ordinal);
        Assert.Contains($"<link rel=\"canonical\" href=\"{OnyxWebFactory.PublicBaseUrl}/{language}/\">", html, StringComparison.Ordinal);
        Assert.Contains($"hreflang=\"ar\" href=\"{OnyxWebFactory.PublicBaseUrl}/ar/\"", html, StringComparison.Ordinal);
        Assert.Contains($"hreflang=\"en\" href=\"{OnyxWebFactory.PublicBaseUrl}/en/\"", html, StringComparison.Ordinal);
        Assert.Contains($"hreflang=\"x-default\" href=\"{OnyxWebFactory.PublicBaseUrl}/ar/\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Arabic_text_is_emitted_as_characters_not_entities()
    {
        var html = await _browser.GetHtmlAsync("/en/");

        Assert.Contains(">العربية</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("&#x627;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pages_are_noindex_until_indexing_is_enabled_for_launch()
    {
        var html = await _browser.GetHtmlAsync("/ar/");

        Assert.Contains("<meta name=\"robots\" content=\"noindex, nofollow\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Accept_Language_offers_a_suggestion_instead_of_redirecting()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/ar/", UriKind.Relative));
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));

        using var response = await _browser.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<aside class=\"notice\" lang=\"en\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/fr/")]
    [InlineData("/ar/no-such-page")]
    public async Task Unknown_languages_and_pages_return_a_localized_404(string path)
    {
        using var response = await _browser.GetPathAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("noindex", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/ar/")]
    public async Task Language_switch_only_returns_to_pages_in_the_chosen_language(string returnPath)
    {
        using var response = await _browser.GetPathAsync($"/en/language?return={Uri.EscapeDataString(returnPath)}");

        Assert.Equal("/en/", response.Location());
    }

    [Fact]
    public async Task Security_headers_include_a_per_request_CSP_nonce()
    {
        using var first = await _browser.GetPathAsync("/ar/");
        using var second = await _browser.GetPathAsync("/ar/");

        var csp = first.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Matches("script-src 'self' 'nonce-[A-Za-z0-9+/=]+'", csp);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.NotEqual(csp, second.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", first.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.False(first.Headers.Contains("Server"));
    }
}
