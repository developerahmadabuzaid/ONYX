using System.Net;
using Onyx.Contracts.Analytics;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;
using Onyx.Web.Analytics;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>
/// GA4 in the browser only with analytics consent, configuration and an eligible public page; CSP opened only for that
/// response; no personal data in what the tag sees; conversion events once; admin analytics screens and their states.
/// </summary>
public sealed class AnalyticsSiteTests : IAsyncLifetime
{
    private const string MeasurementId = "G-SAMPLE1234";

    private readonly OnyxWebFactory _factory = new();
    private HttpClient _browser = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _factory.Settings["Analytics:Ga4MeasurementId"] = MeasurementId;
        _browser = _factory.CreateBrowser();
        _factory.Api.On(HttpMethod.Post, "api/v1/consents", _ => FakeOnyxApi.Json(new { id = Guid.CreateVersion7() }, HttpStatusCode.Accepted));
        _factory.Api.On(HttpMethod.Post, "api/v1/inquiries", _ => FakeOnyxApi.Json(new InquiryAcceptedResponse("ONX-0A1B2C3D"), HttpStatusCode.Created));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _browser.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Without_analytics_consent_there_is_no_tag_and_no_google_origin_in_the_csp()
    {
        using var noChoice = await _browser.GetPathAsync("/en/contact");
        await ConsentAsync(analytics: false);
        using var refused = await _browser.GetPathAsync("/en/contact");

        foreach (var response in new[] { noChoice, refused })
        {
            Assert.DoesNotContain("googletagmanager", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
            Assert.DoesNotContain("google", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task With_consent_the_tag_loads_with_a_nonce_and_only_that_response_allows_googles_origins()
    {
        await ConsentAsync(analytics: true);

        using var response = await _browser.GetPathAsync("/en/contact?utm_source=newsletter&utm_campaign=spring&token=secret-token&email=visitor@example.com");
        var html = await response.Content.ReadAsStringAsync(Token);
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();

        Assert.Contains($"src=\"https://www.googletagmanager.com/gtag/js?id={MeasurementId}\"", html, StringComparison.Ordinal);
        var nonce = csp.Split("'nonce-")[1].Split('\'')[0];
        Assert.Contains($"<script nonce=\"{nonce}\">", html, StringComparison.Ordinal);
        Assert.Contains("script-src 'self' 'nonce-", csp, StringComparison.Ordinal);
        Assert.Contains(AnalyticsTag.ScriptOrigin, csp, StringComparison.Ordinal);
        Assert.Contains("https://*.google-analytics.com", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", csp, StringComparison.Ordinal);

        // Only path and utm parameters reach GA4; tokens, emails and other query values never do.
        Assert.Contains("\"page_location\":\"https://www.onyx.test/en/contact?utm_campaign=spring\\u0026utm_source=newsletter\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", html, StringComparison.Ordinal);
        Assert.DoesNotContain("visitor@example.com", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_and_token_pages_never_load_analytics_even_with_consent()
    {
        await ConsentAsync(analytics: true);

        using var admin = await _browser.GetPathAsync("/admin/account/login");
        using var manage = await _browser.GetPathAsync("/en/manage-booking?token=sample-token");

        foreach (var response in new[] { admin, manage })
        {
            Assert.DoesNotContain("googletagmanager", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
            Assert.DoesNotContain("google", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Without_a_measurement_id_nothing_loads_even_with_consent()
    {
        await using var factory = new OnyxWebFactory();
        using var browser = factory.CreateBrowser();
        factory.Api.On(HttpMethod.Post, "api/v1/consents", _ => FakeOnyxApi.Json(new { id = Guid.CreateVersion7() }, HttpStatusCode.Accepted));
        using (await browser.SubmitFormAsync("/en/", "/en/consent", new Dictionary<string, string> { ["choice"] = "accept-all" }))
        {
        }

        Assert.DoesNotContain("googletagmanager", await browser.GetHtmlAsync("/en/contact"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Withdrawing_analytics_consent_removes_ga_cookies_and_stops_the_tag()
    {
        // A client with manual cookies, so the GA cookies a browser would hold can be sent exactly.
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false,
        });
        using var page = await client.GetAsync(new Uri("/en/", UriKind.Relative), Token);
        var html = await page.Content.ReadAsStringAsync(Token);
        var antiforgeryCookie = page.Headers.GetValues("Set-Cookie").First(cookie => cookie.StartsWith("__Host-onyx.af=", StringComparison.Ordinal)).Split(';')[0];
        using var withdraw = new HttpRequestMessage(HttpMethod.Post, new Uri("/en/consent", UriKind.Relative))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["choice"] = "reject-all", ["__RequestVerificationToken"] = BrowserExtensions.AntiforgeryToken(html)! }),
        };
        withdraw.Headers.Add("Cookie", $"{antiforgeryCookie}; onyx.consent=1.10.{Guid.NewGuid():N}; _ga=GA1.1.123.456; _ga_SAMPLE1234=GS1.1.789");

        using var withdrawal = await client.SendAsync(withdraw, Token);

        Assert.Equal(HttpStatusCode.Redirect, withdrawal.StatusCode);
        var expired = withdrawal.Headers.GetValues("Set-Cookie").Where(cookie => cookie.StartsWith("_ga", StringComparison.Ordinal)).ToList();
        Assert.Contains(expired, cookie => cookie.StartsWith("_ga=;", StringComparison.Ordinal) && cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.Ordinal));
        Assert.Contains(expired, cookie => cookie.StartsWith("_ga_SAMPLE1234=;", StringComparison.Ordinal) && cookie.Contains("domain=localhost", StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith("onyx.consent=1.00.", withdrawal.Headers.GetValues("Set-Cookie").Single(cookie => cookie.StartsWith("onyx.consent=", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_inquiry_sends_one_conversion_event_without_personal_data_and_a_refresh_sends_none()
    {
        await ConsentAsync(analytics: true);

        using var submitted = await _browser.SubmitFormAsync("/en/contact", "/en/contact", new Dictionary<string, string>
        {
            ["Name"] = "Sample Visitor",
            ["Email"] = "visitor@example.com",
            ["Message"] = "We need help with approvals.",
            ["ContactConsent"] = "true",
            ["TurnstileToken"] = "sample-token",
        });
        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);

        var confirmation = await _browser.GetHtmlAsync(submitted.Location()!);
        Assert.Contains("gtag('event',\"inquiry_submitted\",{\"language\":\"en\",\"inquiry_type\":\"contact\"});", confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain("visitor@example.com", ScriptOf(confirmation), StringComparison.Ordinal);
        Assert.DoesNotContain("Sample Visitor", ScriptOf(confirmation), StringComparison.Ordinal);
        Assert.DoesNotContain("ONX-0A1B2C3D", ScriptOf(confirmation), StringComparison.Ordinal);

        var refreshed = await _browser.GetHtmlAsync(submitted.Location()!);
        Assert.DoesNotContain("inquiry_submitted", refreshed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_booked_consultation_sends_its_own_event_once()
    {
        await ConsentAsync(analytics: true);
        var start = _factory.Time.GetUtcNow().AddDays(1);
        _factory.Api.On(HttpMethod.Get, "api/v1/consultations/availability", _ => FakeOnyxApi.Json(
            new AvailabilityResponse(true, "Asia/Riyadh", 30, [new AvailableSlotResponse(start, start.AddMinutes(30), start.ToOffset(TimeSpan.FromHours(3)))])));
        _factory.Api.On(HttpMethod.Post, "api/v1/consultations", _ => FakeOnyxApi.Json(
            new BookingConfirmedResponse("ONX-0B1C2D3E", start, start.AddMinutes(30), start.ToOffset(TimeSpan.FromHours(3)), "Asia/Riyadh"), HttpStatusCode.Created));

        var html = await _browser.GetHtmlAsync("/en/book-a-consultation");
        using var booked = await _browser.PostFormAsync("/en/book-a-consultation", new Dictionary<string, string>
        {
            ["RequestOnly"] = "false", ["StartUtc"] = start.ToString("O"), ["Name"] = "Sample Visitor", ["Email"] = "visitor@example.com", ["ContactConsent"] = "true",
        }, BrowserExtensions.AntiforgeryToken(html));

        var confirmation = await _browser.GetHtmlAsync(booked.Location()!);
        Assert.Contains("gtag('event',\"consultation_booking_confirmed\",{\"language\":\"en\"});", confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain("inquiry_submitted", confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain("consultation_booking_confirmed", await _browser.GetHtmlAsync(booked.Location()!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_consent_a_completed_inquiry_sends_no_event()
    {
        await ConsentAsync(analytics: false);
        using var submitted = await _browser.SubmitFormAsync("/en/contact", "/en/contact", new Dictionary<string, string>
        {
            ["Name"] = "Sample Visitor",
            ["Email"] = "visitor@example.com",
            ["Message"] = "We need help with approvals.",
            ["ContactConsent"] = "true",
            ["TurnstileToken"] = "sample-token",
        });

        Assert.DoesNotContain("inquiry_submitted", await _browser.GetHtmlAsync(submitted.Location()!), StringComparison.Ordinal);
    }

    [Fact]
    public void Only_known_conversion_events_and_inquiry_types_can_be_emitted()
    {
        Assert.Equal(new ConversionEvent("consultation_booking_confirmed", null), ConversionEvent.Parse(ConversionEvent.ForBooking()));
        Assert.Equal(new ConversionEvent("inquiry_submitted", "start_project"), ConversionEvent.Parse(ConversionEvent.ForInquiry("StartProject")));
        Assert.Null(ConversionEvent.Parse("inquiry_submitted:</script><script>alert(1)"));
        Assert.Null(ConversionEvent.Parse("purchase"));
        Assert.Null(ConversionEvent.Parse(null));
        var script = AnalyticsTag.Script(MeasurementId, "https://www.onyx.test/en/</script>", "https://other.example/</script>", "localhost", null, "en");
        Assert.DoesNotContain("</script>", script, StringComparison.Ordinal);
        Assert.Equal("https://other.example/", AnalyticsTag.PageReferrer("https://www.onyx.test", "https://other.example/path?q=secret"));
        Assert.Equal("https://www.onyx.test/en/search", AnalyticsTag.PageReferrer("https://www.onyx.test", "https://www.onyx.test/en/search?q=secret"));
    }

    [Fact]
    public async Task Admin_analytics_shows_states_never_zeroes_and_only_permitted_tabs()
    {
        await SignInAsync(Permissions.Analytics.View);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/analytics/overview", _ => FakeOnyxApi.Json(new AnalyticsReportResponse(
            "overview", new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 6),
            [
                new AnalyticsDataStatusResponse("analytics.ga4", "NotConfigured", false, false, null, null, null, null, null, false),
                new AnalyticsDataStatusResponse("onyx", "Ok", false, false, null, null, null, null, null, false),
            ],
            [
                new AnalyticsSectionResponse("webDaily", "analytics.ga4", ["activeUsers", "sessions", "engagedSessions", "pageViews"], []),
                new AnalyticsSectionResponse("firstParty", "onyx", ["count"], [new AnalyticsRowResponse("inquiries", null, [3])]),
            ],
            ["Analytics.Note.SeparateDatasets"])));

        var html = await _browser.GetHtmlAsync("/admin/analytics");

        Assert.Contains("Not configured", html, StringComparison.Ordinal);
        Assert.Contains("Not connected: no data is available (nothing is shown as zero).", html, StringComparison.Ordinal);
        Assert.Contains("2026-09-09 – 2026-10-06", html, StringComparison.Ordinal);
        Assert.Contains("separate datasets", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/analytics/funnel\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/analytics/search-queries\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/analytics/integrations\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("googletagmanager", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await _browser.GetPathAsync("/admin/analytics/not-a-report")).StatusCode);
    }

    [Fact]
    public async Task Integration_status_shows_failures_and_sync_requests_need_an_antiforgery_token()
    {
        await SignInAsync(Permissions.ProviderConnections.Manage);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/analytics-integrations", _ => FakeOnyxApi.Json(new List<AnalyticsIntegrationResponse>
        {
            new("analytics.ga4", "Error", _factory.Time.GetUtcNow(), null, "Analytics.Unauthorized",
                new AnalyticsDataStatusResponse("analytics.ga4", "Failed", true, false, null, _factory.Time.GetUtcNow(), "Analytics.Unauthorized",
                    new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), false), "analytics-ga4-sync"),
        }));
        _factory.Api.On(HttpMethod.Post, "api/v1/admin/analytics-integrations/analytics.ga4/sync", _ => new HttpResponseMessage(HttpStatusCode.Accepted));

        var page = await _browser.GetHtmlAsync("/admin/analytics/integrations");
        Assert.Contains("Failed (the last good data is shown)", page, StringComparison.Ordinal);
        Assert.Contains("stale", page, StringComparison.Ordinal);
        Assert.Contains("Analytics.Unauthorized", page, StringComparison.Ordinal);

        using var forged = await _browser.PostFormAsync("/admin/analytics/integrations/analytics.ga4/sync", new Dictionary<string, string>(), null);
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Empty(_factory.Api.RequestsTo("api/v1/admin/analytics-integrations/analytics.ga4/sync"));

        using var requested = await _browser.PostFormAsync("/admin/analytics/integrations/analytics.ga4/sync", new Dictionary<string, string>(), BrowserExtensions.AntiforgeryToken(page));
        Assert.Equal(HttpStatusCode.Redirect, requested.StatusCode);
        Assert.Single(_factory.Api.RequestsTo("api/v1/admin/analytics-integrations/analytics.ga4/sync"));
    }

    private async Task ConsentAsync(bool analytics)
    {
        using var response = await _browser.SubmitFormAsync("/en/", "/en/consent", new Dictionary<string, string> { ["choice"] = analytics ? "accept-all" : "reject-all" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private async Task SignInAsync(params string[] permissions)
    {
        AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", permissions));
        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    private static string ScriptOf(string html)
    {
        var start = html.IndexOf("gtag('js'", StringComparison.Ordinal);
        return start < 0 ? string.Empty : html[start..html.IndexOf("</script>", start, StringComparison.Ordinal)];
    }
}
