using System.Net;
using Onyx.Contracts.Identity;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

public sealed class AdminAuthenticationTests : IAsyncLifetime
{
    private const string AdminCookie = "__Host-onyx.admin";

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
    public async Task Admin_pages_require_sign_in()
    {
        using var response = await _browser.GetPathAsync("/admin/notifications");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith("https://localhost/admin/account/login?returnUrl=%2Fadmin%2Fnotifications", response.Location(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sign_in_issues_a_hardened_session_cookie_that_keeps_tokens_out_of_the_page()
    {
        var tokens = AdminApiStubs.SignInSucceeds(_factory);

        using var login = await AdminApiStubs.LogInAsync(_browser);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/admin", login.Location());
        var cookie = Assert.Single(login.SetCookies(AdminCookie));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.True(cookie.Split(';')[0].Length < 4096, "The admin cookie must stay under 4 KB.");
        Assert.Empty(login.SetCookies(AdminCookie + "C1"));
        Assert.DoesNotContain(tokens.AccessToken, cookie, StringComparison.Ordinal);

        using var dashboard = await _browser.GetPathAsync("/admin");
        var html = await dashboard.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        Assert.Contains(AdminApiStubs.DisplayName, html, StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.AccessToken, html, StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.RefreshToken, html, StringComparison.Ordinal);
        Assert.Equal("no-store", dashboard.Headers.CacheControl?.ToString());
        Assert.All(_factory.Api.RequestsTo("api/v1/auth/me"), request => Assert.Equal(tokens.AccessToken, request.BearerToken));
    }

    [Fact]
    public async Task The_admin_UI_follows_the_admins_preferred_language()
    {
        AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("ar"));
        using var _ = await AdminApiStubs.LogInAsync(_browser);

        var html = await _browser.GetHtmlAsync("/admin");

        Assert.Contains("<html lang=\"ar\" dir=\"rtl\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_credentials_show_a_generic_error()
    {
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/login", _ => FakeOnyxApi.Problem(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials"));

        using var response = await AdminApiStubs.LogInAsync(_browser);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.SetCookies(AdminCookie));
        Assert.Contains("class=\"errors\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_without_an_antiforgery_token_is_rejected()
    {
        using var response = await _browser.PostFormAsync(
            "/admin/account/login",
            new Dictionary<string, string> { ["Email"] = AdminApiStubs.Email, ["Password"] = AdminApiStubs.Password },
            antiforgeryToken: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Api.RequestsTo("api/v1/auth/login"));
    }

    [Theory]
    [InlineData("/admin/notifications", "/admin/notifications")]
    [InlineData("https://evil.example/admin", "/admin")]
    [InlineData("//evil.example/admin", "/admin")]
    [InlineData("/en/", "/admin")]
    public async Task After_sign_in_only_local_admin_return_urls_are_followed(string returnUrl, string expected)
    {
        AdminApiStubs.SignInSucceeds(_factory);

        using var response = await AdminApiStubs.LogInAsync(_browser, returnUrl);

        Assert.Equal(expected, response.Location());
    }

    [Fact]
    public async Task Two_factor_challenge_stays_server_side_and_completes_sign_in()
    {
        const string challenge = "challenge-token-that-must-never-reach-the-browser";
        var tokens = AdminApiStubs.Tokens(_factory.Time);
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/login", _ => FakeOnyxApi.Json(new LoginResponse(LoginStatus.TwoFactorRequired, null, challenge, null)));
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/login/two-factor", request =>
            request.BodyAs<TwoFactorLoginRequest>() is { ChallengeToken: challenge, Code: "123456" }
                ? FakeOnyxApi.Json(tokens)
                : FakeOnyxApi.Problem(HttpStatusCode.Unauthorized, "Auth.InvalidCode"));
        AdminApiStubs.AuthenticatedEndpoints(_factory, () => tokens.AccessToken);

        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal("/admin/account/two-factor", login.Location());
        Assert.Empty(login.SetCookies(AdminCookie));

        var page = await _browser.GetHtmlAsync("/admin/account/two-factor");
        Assert.DoesNotContain(challenge, page, StringComparison.Ordinal);

        using var wrong = await _browser.PostFormAsync("/admin/account/two-factor", new Dictionary<string, string> { ["Code"] = "000000" }, BrowserExtensions.AntiforgeryToken(page));
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        Assert.Empty(wrong.SetCookies(AdminCookie));

        using var right = await _browser.PostFormAsync("/admin/account/two-factor", new Dictionary<string, string> { ["Code"] = "123456" }, BrowserExtensions.AntiforgeryToken(page));
        Assert.Equal("/admin", right.Location());
        Assert.Single(right.SetCookies(AdminCookie));
    }

    [Fact]
    public async Task Required_enrollment_shows_the_key_then_recovery_codes_once()
    {
        const string enrollmentToken = "enrollment-token-that-must-never-reach-the-browser";
        var tokens = AdminApiStubs.Tokens(_factory.Time);
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/login", _ => FakeOnyxApi.Json(new LoginResponse(LoginStatus.TwoFactorEnrollmentRequired, null, enrollmentToken, null)));
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/two-factor/enrollment/start", _ =>
            FakeOnyxApi.Json(new TwoFactorEnrollmentStartResponse("SAMPLEKEYSAMPLEKEY", "otpauth://totp/Onyx:sample?secret=SAMPLEKEYSAMPLEKEY")));
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/two-factor/enrollment/complete", request =>
            request.BodyAs<TwoFactorEnrollmentCompleteRequest>().EnrollmentToken == enrollmentToken
                ? FakeOnyxApi.Json(new TwoFactorEnrollmentCompleteResponse(["sample-recovery-1", "sample-recovery-2"], tokens))
                : FakeOnyxApi.Problem(HttpStatusCode.Unauthorized, "Auth.InvalidChallenge"));
        AdminApiStubs.AuthenticatedEndpoints(_factory, () => tokens.AccessToken);

        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal("/admin/account/two-factor/setup", login.Location());

        var setup = await _browser.GetHtmlAsync("/admin/account/two-factor/setup");
        Assert.Contains("SAMPLEKEYSAMPLEKEY", setup, StringComparison.Ordinal);
        Assert.DoesNotContain(enrollmentToken, setup, StringComparison.Ordinal);

        using var completed = await _browser.PostFormAsync(
            "/admin/account/two-factor/setup",
            new Dictionary<string, string> { ["Code"] = "123456", ["SharedKey"] = "SAMPLEKEYSAMPLEKEY", ["AuthenticatorUri"] = "otpauth://x" },
            BrowserExtensions.AntiforgeryToken(setup));
        var codes = await completed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Contains("sample-recovery-1", codes, StringComparison.Ordinal);
        Assert.Single(completed.SetCookies(AdminCookie));
        Assert.Equal("no-store", completed.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task An_expiring_access_token_is_refreshed_server_side()
    {
        var current = AdminApiStubs.Tokens(_factory.Time);
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/login", _ => FakeOnyxApi.Json(new LoginResponse(LoginStatus.Succeeded, current, null, null)));
        var issued = current;
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/refresh", request =>
        {
            Assert.Equal(issued.RefreshToken, request.BodyAs<RefreshTokenRequest>().RefreshToken);
            current = AdminApiStubs.Tokens(_factory.Time);
            return FakeOnyxApi.Json(current);
        });
        AdminApiStubs.AuthenticatedEndpoints(_factory, () => current.AccessToken);
        using var _ = await AdminApiStubs.LogInAsync(_browser);

        _factory.Time.Advance(TimeSpan.FromMinutes(9.5));
        using var response = await _browser.GetPathAsync("/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(_factory.Api.RequestsTo("api/v1/auth/refresh"));
        Assert.NotEqual(issued.AccessToken, current.AccessToken);
        Assert.Single(response.SetCookies(AdminCookie));

        using var again = await _browser.GetPathAsync("/admin");
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Single(_factory.Api.RequestsTo("api/v1/auth/refresh"));
    }

    [Fact]
    public async Task A_refused_refresh_ends_the_session()
    {
        AdminApiStubs.SignInSucceeds(_factory);
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/refresh", _ => FakeOnyxApi.Problem(HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken"));
        using var _ = await AdminApiStubs.LogInAsync(_browser);

        _factory.Time.Advance(TimeSpan.FromMinutes(9.5));
        using var response = await _browser.GetPathAsync("/admin");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/admin/account/login", response.Location(), StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", Assert.Single(response.SetCookies(AdminCookie)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_session_revoked_at_the_API_signs_the_admin_out()
    {
        var revoked = false;
        var tokens = AdminApiStubs.SignInSucceeds(_factory);
        AdminApiStubs.AuthenticatedEndpoints(_factory, () => revoked ? "revoked" : tokens.AccessToken);
        using var _ = await AdminApiStubs.LogInAsync(_browser);

        revoked = true;
        using var response = await _browser.GetPathAsync("/admin/notifications");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/account/login?returnUrl=%2Fadmin%2Fnotifications", response.Location());
        Assert.Contains("expires=Thu, 01 Jan 1970", Assert.Single(response.SetCookies(AdminCookie)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_and_clears_the_cookie()
    {
        var tokens = AdminApiStubs.SignInSucceeds(_factory);
        using var _ = await AdminApiStubs.LogInAsync(_browser);
        var dashboard = await _browser.GetHtmlAsync("/admin");

        using var response = await _browser.PostFormAsync("/admin/account/logout", new Dictionary<string, string>(), BrowserExtensions.AntiforgeryToken(dashboard));

        Assert.Equal("/admin/account/login", response.Location());
        Assert.Equal(tokens.RefreshToken, Assert.Single(_factory.Api.RequestsTo("api/v1/auth/logout")).BodyAs<LogoutRequest>().RefreshToken);
        Assert.Contains("expires=Thu, 01 Jan 1970", Assert.Single(response.SetCookies(AdminCookie)), StringComparison.Ordinal);
        using var after = await _browser.GetPathAsync("/admin");
        Assert.Equal(HttpStatusCode.Found, after.StatusCode);
    }

    [Fact]
    public async Task An_unreachable_API_shows_a_service_message_instead_of_failing()
    {
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/login", _ => throw new HttpRequestException("connection refused"));

        using var response = await AdminApiStubs.LogInAsync(_browser);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"errors\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Empty(response.SetCookies(AdminCookie));
    }

    [Fact]
    public async Task An_API_outage_during_refresh_keeps_the_session()
    {
        AdminApiStubs.SignInSucceeds(_factory);
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/refresh", _ => throw new HttpRequestException("connection refused"));
        using var _ = await AdminApiStubs.LogInAsync(_browser);

        _factory.Time.Advance(TimeSpan.FromMinutes(9.5));
        using var response = await _browser.GetPathAsync("/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.SetCookies(AdminCookie));
    }

    [Fact]
    public async Task Notifications_page_lists_items_localized_by_type()
    {
        AdminApiStubs.SignInSucceeds(_factory);
        using var _ = await AdminApiStubs.LogInAsync(_browser);

        var html = await _browser.GetHtmlAsync("/admin/notifications");

        Assert.Contains("Scheduled job failed", html, StringComparison.Ordinal);
        Assert.Contains("Unread notifications: 2", html, StringComparison.Ordinal);
    }
}
