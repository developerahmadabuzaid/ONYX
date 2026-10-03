using System.Net;
using Onyx.Contracts.Identity;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>Emailed reset/invitation links: the token leaves the address bar immediately and never appears in a page.</summary>
public sealed class TokenLinkTests : IAsyncLifetime
{
    private const string LinkToken = "c2FtcGxlLWxpbmstdG9rZW4tZm9yLXRlc3RzLW9ubHk";

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

    [Theory]
    [InlineData("reset-password", "api/v1/auth/password/reset")]
    [InlineData("accept-invitation", "api/v1/auth/invitations/accept")]
    public async Task Link_token_is_moved_out_of_the_url_and_submitted_server_side(string page, string apiPath)
    {
        var userId = Guid.CreateVersion7();
        _factory.Api.On(HttpMethod.Post, apiPath, _ => FakeOnyxApi.NoContent());

        using var landing = await _browser.GetPathAsync($"/admin/account/{page}?userId={userId}&token={LinkToken}");

        Assert.Equal(HttpStatusCode.Redirect, landing.StatusCode);
        Assert.Equal($"/admin/account/{page}", landing.Location());
        Assert.Equal("no-referrer", landing.Headers.GetValues("Referrer-Policy").Single());

        using var form = await _browser.GetPathAsync($"/admin/account/{page}");
        var html = await form.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        Assert.DoesNotContain(LinkToken, html, StringComparison.Ordinal);
        Assert.Equal("no-referrer", form.Headers.GetValues("Referrer-Policy").Single());
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);

        using var submitted = await _browser.PostFormAsync(
            $"/admin/account/{page}",
            new Dictionary<string, string> { ["NewPassword"] = "a long sample passphrase", ["ConfirmPassword"] = "a long sample passphrase" },
            BrowserExtensions.AntiforgeryToken(html));

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var sent = Assert.Single(_factory.Api.RequestsTo(apiPath)).BodyAs<ResetPasswordRequest>();
        Assert.Equal(userId, sent.UserId);
        Assert.Equal(LinkToken, sent.Token);
        Assert.Equal("a long sample passphrase", sent.NewPassword);

        // Single use within the browser too: the stored token is gone after success.
        using var reused = await _browser.GetPathAsync($"/admin/account/{page}");
        Assert.DoesNotContain("NewPassword", await reused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_expired_or_invalid_token_shows_the_invalid_link_message()
    {
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/password/reset", _ => FakeOnyxApi.Problem(HttpStatusCode.BadRequest, "Auth.InvalidToken"));
        using var english = await _browser.GetPathAsync("/admin/language?to=en");
        using var _ = await _browser.GetPathAsync($"/admin/account/reset-password?userId={Guid.CreateVersion7()}&token={LinkToken}");
        var html = await _browser.GetHtmlAsync("/admin/account/reset-password");

        using var submitted = await _browser.PostFormAsync(
            "/admin/account/reset-password",
            new Dictionary<string, string> { ["NewPassword"] = "a long sample passphrase", ["ConfirmPassword"] = "a long sample passphrase" },
            BrowserExtensions.AntiforgeryToken(html));

        var body = await submitted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("invalid or has expired", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NewPassword", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("token=short")]
    [InlineData("token=has%20spaces%20and%3Cscript%3E")]
    [InlineData("userId=not-a-guid&token=c2FtcGxlLWxpbmstdG9rZW4tZm9yLXRlc3RzLW9ubHk")]
    [InlineData("")]
    public async Task Malformed_links_are_not_accepted(string query)
    {
        using var response = await _browser.GetPathAsync($"/admin/account/reset-password?{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("NewPassword", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forgot_password_always_shows_the_same_confirmation()
    {
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/password/forgot", _ => new HttpResponseMessage(HttpStatusCode.Accepted));

        using var response = await _browser.SubmitFormAsync("/admin/account/forgot-password", "/admin/account/forgot-password", new Dictionary<string, string>
        {
            ["Email"] = "someone@example.test",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("someone@example.test", Assert.Single(_factory.Api.RequestsTo("api/v1/auth/password/forgot")).BodyAs<ForgotPasswordRequest>().Email);
    }
}
