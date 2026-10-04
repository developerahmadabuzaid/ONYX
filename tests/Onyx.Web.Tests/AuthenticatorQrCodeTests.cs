using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Onyx.Contracts.Identity;
using Onyx.Web.Areas.Admin;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>QR enrolment: renders the current attempt's otpauth URI only, keeps the text key, and leaks nothing.</summary>
public sealed partial class AuthenticatorQrCodeTests : IAsyncLifetime
{
    private const string EnrollmentToken = "enrollment-token-that-must-never-reach-the-browser";
    private const string SharedKey = "SAMPLEKEYSAMPLEKEYSAMPLEKEYSAMPL";
    private const string Uri = "otpauth://totp/Onyx%20Vanguard%20Solutions:sample%40example.test?secret=SAMPLEKEYSAMPLEKEYSAMPLEKEYSAMPL&issuer=Onyx%20Vanguard%20Solutions&digits=6";

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
    public void The_QR_is_an_SVG_data_uri_of_exactly_the_given_otpauth_uri()
    {
        var dataUri = AuthenticatorQrCode.ToDataUri(Uri);

        Assert.NotNull(dataUri);
        Assert.StartsWith("data:image/svg+xml;base64,", dataUri, StringComparison.Ordinal);
        var svg = Encoding.UTF8.GetString(Convert.FromBase64String(dataUri["data:image/svg+xml;base64,".Length..]));
        Assert.Contains("<svg", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", svg, StringComparison.OrdinalIgnoreCase);

        // Deterministic for the same URI, different for a different URI: it encodes this attempt's URI and nothing else.
        Assert.Equal(dataUri, AuthenticatorQrCode.ToDataUri(Uri));
        Assert.NotEqual(dataUri, AuthenticatorQrCode.ToDataUri(Uri.Replace("SAMPLEKEY", "OTHERKEYS", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/?secret=ABC")]
    [InlineData("javascript:alert(1)")]
    [InlineData("otpauth://hotp/Onyx:sample?secret=ABC&counter=0")]
    [InlineData("OTPAUTH://TOTP/Onyx:sample?secret=ABC")]
    public void Anything_but_a_TOTP_otpauth_uri_gets_no_QR(string? value) =>
        Assert.Null(AuthenticatorQrCode.ToDataUri(value));

    [Fact]
    public void Oversized_values_get_no_QR() =>
        Assert.Null(AuthenticatorQrCode.ToDataUri("otpauth://totp/x?secret=" + new string('A', 600)));

    [Fact]
    public async Task The_setup_page_shows_the_QR_of_the_current_attempt_plus_the_text_key_fallback()
    {
        StubEnrollment(Uri);
        await StartEnrollmentAsync();

        using var response = await _browser.GetPathAsync("/admin/account/two-factor/setup");
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AuthenticatorQrCode.ToDataUri(Uri), QrSource(html));
        Assert.Contains(SharedKey, html, StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Single(_factory.Api.RequestsTo("api/v1/auth/two-factor/enrollment/start"));
    }

    [Fact]
    public async Task After_a_wrong_code_the_page_shows_the_same_QR_without_starting_a_new_attempt()
    {
        StubEnrollment(Uri);
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/two-factor/enrollment/complete", _ => FakeOnyxApi.Problem(HttpStatusCode.Unauthorized, "Auth.InvalidCode"));
        await StartEnrollmentAsync();
        var setup = await _browser.GetHtmlAsync("/admin/account/two-factor/setup");

        using var retry = await _browser.PostFormAsync(
            "/admin/account/two-factor/setup",
            new Dictionary<string, string> { ["Code"] = "000000", ["SharedKey"] = SharedKey, ["AuthenticatorUri"] = Uri, ["QrCodeDataUri"] = "data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=" },
            BrowserExtensions.AntiforgeryToken(setup));
        var html = await retry.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(AuthenticatorQrCode.ToDataUri(Uri), QrSource(html));
        Assert.Single(_factory.Api.RequestsTo("api/v1/auth/two-factor/enrollment/start"));
        Assert.Equal("no-store", retry.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task A_non_TOTP_uri_from_the_API_shows_only_the_text_key()
    {
        StubEnrollment("https://evil.example/");
        await StartEnrollmentAsync();

        var html = await _browser.GetHtmlAsync("/admin/account/two-factor/setup");

        Assert.Null(QrSource(html));
        Assert.Contains(SharedKey, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_secret_uri_QR_OTP_or_recovery_code_reaches_the_logs()
    {
        var tokens = AdminApiStubs.Tokens(_factory.Time);
        StubEnrollment(Uri);
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/two-factor/enrollment/complete", _ =>
            FakeOnyxApi.Json(new TwoFactorEnrollmentCompleteResponse(["sample-recovery-code-1"], tokens)));
        AdminApiStubs.AuthenticatedEndpoints(_factory, () => tokens.AccessToken);
        await StartEnrollmentAsync();
        var setup = await _browser.GetHtmlAsync("/admin/account/two-factor/setup");

        using var completed = await _browser.PostFormAsync(
            "/admin/account/two-factor/setup",
            new Dictionary<string, string> { ["Code"] = "271828", ["SharedKey"] = SharedKey, ["AuthenticatorUri"] = Uri },
            BrowserExtensions.AntiforgeryToken(setup));
        Assert.Contains("sample-recovery-code-1", await completed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        var logs = string.Join('\n', _factory.Logs.Messages);
        Assert.NotEmpty(_factory.Logs.Messages);
        Assert.DoesNotContain(SharedKey, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("otpauth", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:image/svg", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("271828", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("sample-recovery-code-1", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(EnrollmentToken, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.AccessToken, logs, StringComparison.Ordinal);
    }

    private void StubEnrollment(string authenticatorUri)
    {
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/login", _ =>
            FakeOnyxApi.Json(new LoginResponse(LoginStatus.TwoFactorEnrollmentRequired, null, EnrollmentToken, null)));
        _factory.Api.On(HttpMethod.Post, "api/v1/auth/two-factor/enrollment/start", _ =>
            FakeOnyxApi.Json(new TwoFactorEnrollmentStartResponse(SharedKey, authenticatorUri)));
    }

    private async Task StartEnrollmentAsync()
    {
        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal("/admin/account/two-factor/setup", login.Location());
    }

    private static string? QrSource(string html) =>
        QrImage().Match(html) is { Success: true } match ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value) : null;

    // Razor HTML-encodes the attribute (e.g. "+" as "&#x2B;"); browsers decode it, so the test decodes it too.
    [GeneratedRegex("<img src=\"(data:image/svg[^\"]+)\"")]
    private static partial Regex QrImage();
}
