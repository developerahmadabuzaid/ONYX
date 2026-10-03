using System.Net;
using Onyx.Contracts.Privacy;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

public sealed class ConsentTests : IAsyncLifetime
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
    public async Task The_banner_shows_until_a_choice_is_made_and_the_choice_is_recorded_by_the_API()
    {
        _factory.Api.On(HttpMethod.Post, "api/v1/consents", _ => FakeOnyxApi.Json(new { id = Guid.CreateVersion7() }, HttpStatusCode.Accepted));
        Assert.Contains("class=\"consent\"", await _browser.GetHtmlAsync("/en/"), StringComparison.Ordinal);

        using var response = await _browser.SubmitFormAsync("/en/", "/en/consent", new Dictionary<string, string>
        {
            ["choice"] = "custom",
            ["analytics"] = "true",
            ["returnUrl"] = "/en/",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/en/", response.Location());
        var recorded = Assert.Single(_factory.Api.RequestsTo("api/v1/consents")).BodyAs<RecordCookieConsentRequest>();
        Assert.True(recorded.Analytics);
        Assert.False(recorded.Marketing);
        Assert.Equal("1", recorded.PolicyVersion);
        Assert.Equal("en", recorded.LanguageCode);
        Assert.NotEqual(Guid.Empty, recorded.ConsentId);

        var cookie = Assert.Single(response.SetCookies("onyx.consent"));
        Assert.StartsWith($"onyx.consent=1.10.{recorded.ConsentId:N}", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"consent\"", await _browser.GetHtmlAsync("/en/"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_choice_is_honoured_even_when_the_receipt_cannot_be_stored()
    {
        _factory.Api.On(HttpMethod.Post, "api/v1/consents", _ => FakeOnyxApi.Problem(HttpStatusCode.ServiceUnavailable, "Test.Down"));

        using var response = await _browser.SubmitFormAsync("/ar/", "/ar/consent", new Dictionary<string, string> { ["choice"] = "reject-all" });

        Assert.StartsWith("onyx.consent=1.00.", Assert.Single(response.SetCookies("onyx.consent")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_consent_post_without_an_antiforgery_token_is_rejected()
    {
        using var response = await _browser.PostFormAsync("/ar/consent", new Dictionary<string, string> { ["choice"] = "accept-all" }, antiforgeryToken: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Api.RequestsTo("api/v1/consents"));
    }

    [Fact]
    public async Task Return_urls_outside_the_current_language_are_ignored()
    {
        _factory.Api.On(HttpMethod.Post, "api/v1/consents", _ => FakeOnyxApi.Json(new { id = Guid.CreateVersion7() }, HttpStatusCode.Accepted));

        using var response = await _browser.SubmitFormAsync("/ar/", "/ar/consent", new Dictionary<string, string>
        {
            ["choice"] = "accept-all",
            ["returnUrl"] = "https://evil.example/ar/",
        });

        Assert.Equal("/ar/", response.Location());
    }
}
