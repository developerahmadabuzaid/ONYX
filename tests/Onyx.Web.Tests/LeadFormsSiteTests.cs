using System.Net;
using System.Text.Json;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Content;
using Onyx.Contracts.Leads;
using Onyx.Web.Attribution;
using Onyx.Web.Consent;
using Onyx.Web.Leads;
using Onyx.Web.Localization;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>Public conversion pages: forms post to the API only, Turnstile is allowed only where used, attribution follows consent.</summary>
public sealed class LeadFormsSiteTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Guid ServiceId = Guid.CreateVersion7();

    private readonly OnyxWebFactory _factory = new();
    private HttpClient _browser = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _browser = _factory.CreateBrowser();
        _factory.Api.On(HttpMethod.Post, "api/v1/inquiries", _ => FakeOnyxApi.Json(new InquiryAcceptedResponse("ONX-0A1B2C3D"), HttpStatusCode.Created));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _browser.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Form_pages_render_the_challenge_and_only_they_allow_its_origin()
    {
        using var contact = await _browser.GetPathAsync("/en/contact");
        var html = await contact.Content.ReadAsStringAsync(Token);
        var csp = contact.Headers.GetValues("Content-Security-Policy").Single();

        Assert.Equal(HttpStatusCode.OK, contact.StatusCode);
        Assert.Contains("class=\"cf-turnstile\" data-sitekey=\"test-site-key\" data-action=\"inquiry\"", html, StringComparison.Ordinal);
        Assert.Contains("script-src 'self' 'nonce-", csp, StringComparison.Ordinal);
        Assert.Contains("https://challenges.cloudflare.com", csp, StringComparison.Ordinal);
        Assert.Contains("name=\"Website\"", html, StringComparison.Ordinal);

        using var other = await _browser.GetPathAsync("/admin/account/access-denied");
        Assert.DoesNotContain("challenges.cloudflare.com", other.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);

        var arabic = await _browser.GetHtmlAsync("/ar/" + Uri.EscapeDataString("تواصل-معنا"));
        Assert.Contains("lang=\"ar\" dir=\"rtl\"", arabic, StringComparison.Ordinal);
        Assert.Contains("hreflang=\"en\" href=\"https://www.onyx.test/en/contact\"", arabic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_site_key_the_form_says_submission_is_unavailable_instead_of_offering_a_failing_submit()
    {
        await using var factory = new OnyxWebFactory();
        factory.Settings["Turnstile:SiteKey"] = string.Empty;
        using var browser = factory.CreateBrowser();

        var html = await browser.GetHtmlAsync("/en/contact");

        Assert.DoesNotContain("cf-turnstile", html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"submit\"", html, StringComparison.Ordinal);
        Assert.Contains("Online submission is not available right now", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_contact_from_a_service_becomes_a_service_inquiry_with_session_context_and_redirects_to_the_confirmation()
    {
        using var response = await _browser.SubmitFormAsync($"/en/contact?service={ServiceId:D}&utm_source=newsletter", "/en/contact", new Dictionary<string, string>
        {
            ["Name"] = "Sample Visitor",
            ["Email"] = "visitor@example.com",
            ["Message"] = "We need help with approvals.",
            ["ContactConsent"] = "true",
            ["ServiceId"] = ServiceId.ToString("D"),
            ["Context"] = AttributionCookies.Encode(new TouchPointDto("/en/contact", null, "newsletter", null, null, null, null, null)),
            ["cf-turnstile-response"] = "solved-token",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/en/contact/sent", response.Location());
        var sent = JsonSerializer.Deserialize<SubmitInquiryRequest>(Assert.Single(_factory.Api.RequestsTo("api/v1/inquiries")).Body!, Json)!;
        Assert.Equal(InquiryTypes.ServiceInquiry, sent.Type);
        Assert.Equal(ServiceId, sent.Form.ServiceId);
        Assert.Equal("solved-token", sent.Form.CaptchaToken);
        Assert.Equal("en", sent.Form.LanguageCode);
        Assert.True(sent.Form.ContactConsent);
        Assert.False(sent.Form.Attribution!.AnalyticsConsent);
        Assert.Null(sent.Form.Attribution.FirstTouch);
        Assert.Equal("newsletter", sent.Form.Attribution.LastTouch!.UtmSource);
        Assert.Equal("/en/contact", sent.Form.Attribution.SubmissionPath);

        var confirmation = await _browser.GetHtmlAsync("/en/contact/sent");
        Assert.Contains("ONX-0A1B2C3D", confirmation, StringComparison.Ordinal);
        Assert.Contains("noindex", confirmation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_consent_is_refused_locally_and_api_problems_are_shown_without_losing_the_form()
    {
        using var noConsent = await _browser.SubmitFormAsync("/en/contact", "/en/contact", new Dictionary<string, string>
        {
            ["Name"] = "Sample Visitor",
            ["Email"] = "visitor@example.com",
            ["Message"] = "Hello",
        });
        Assert.Equal(HttpStatusCode.OK, noConsent.StatusCode);
        Assert.Empty(_factory.Api.RequestsTo("api/v1/inquiries"));

        _factory.Api.On(HttpMethod.Post, "api/v1/inquiries", _ => FakeOnyxApi.Problem(HttpStatusCode.BadRequest, "Captcha.Failed"));
        using var rejected = await _browser.SubmitFormAsync("/en/contact", "/en/contact", new Dictionary<string, string>
        {
            ["Name"] = "Sample Visitor",
            ["Email"] = "visitor@example.com",
            ["Message"] = "Hello",
            ["ContactConsent"] = "true",
        });
        var html = await rejected.Content.ReadAsStringAsync(Token);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("The anti-abuse check did not pass", html, StringComparison.Ordinal);
        Assert.Contains("value=\"visitor@example.com\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_project_wizard_checks_each_step_and_sends_the_versioned_answers()
    {
        var step1 = await _browser.GetHtmlAsync("/en/start-a-project");
        var token = BrowserExtensions.AntiforgeryToken(step1);
        Assert.Contains("Step 1 of 3", step1, StringComparison.Ordinal);

        using var missing = await _browser.PostFormAsync("/en/start-a-project", new Dictionary<string, string> { ["Step"] = "1", ["Command"] = "next" }, token);
        Assert.Contains("Choose at least one area.", await missing.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        using var step2 = await _browser.PostFormAsync("/en/start-a-project", new Dictionary<string, string>
        {
            ["Step"] = "1", ["Command"] = "next", ["Pillars"] = "ai",
        }, token);
        Assert.Contains("Step 2 of 3", await step2.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        using var submitted = await _browser.PostFormAsync("/en/start-a-project", new Dictionary<string, string>
        {
            ["Step"] = "3", ["Command"] = "submit", ["Pillars"] = "ai", ["Message"] = "Automate onboarding.", ["Timeline"] = "1-3-months",
            ["OrganizationSize"] = "invented-size", ["Name"] = "Sample Visitor", ["Email"] = "visitor@example.com", ["ContactConsent"] = "true",
            ["cf-turnstile-response"] = "solved",
        }, token);

        Assert.Equal("/en/start-a-project/sent", submitted.Location());
        var sent = JsonSerializer.Deserialize<SubmitInquiryRequest>(Assert.Single(_factory.Api.RequestsTo("api/v1/inquiries")).Body!, Json)!;
        Assert.Equal(InquiryTypes.StartProject, sent.Type);
        Assert.Equal(1, sent.Form.AnswersVersion);
        Assert.Equal(["ai"], sent.Form.Answers!["pillars"]);
        Assert.Equal(["1-3-months"], sent.Form.Answers["timeline"]);
        Assert.False(sent.Form.Answers.ContainsKey("organization-size"));
    }

    [Fact]
    public async Task Without_online_slots_the_booking_page_sends_a_consultation_request()
    {
        _factory.Api.On(HttpMethod.Get, "api/v1/consultations/availability", _ => FakeOnyxApi.Json(new AvailabilityResponse(false, "Asia/Riyadh", 0, [])));

        var html = await _browser.GetHtmlAsync("/en/book-a-consultation");
        Assert.Contains("Online booking is not available at the moment", html, StringComparison.Ordinal);
        Assert.Contains("name=\"RequestOnly\" value=\"true\"", html, StringComparison.Ordinal);

        using var response = await _browser.PostFormAsync("/en/book-a-consultation", new Dictionary<string, string>
        {
            ["RequestOnly"] = "true", ["Message"] = "Discuss AI options.", ["Name"] = "Sample Visitor", ["Email"] = "visitor@example.com", ["ContactConsent"] = "true",
        }, BrowserExtensions.AntiforgeryToken(html));

        Assert.Equal("/en/book-a-consultation/booked", response.Location());
        var sent = JsonSerializer.Deserialize<SubmitInquiryRequest>(Assert.Single(_factory.Api.RequestsTo("api/v1/inquiries")).Body!, Json)!;
        Assert.Equal(InquiryTypes.RequestConsultation, sent.Type);
    }

    [Fact]
    public async Task Booking_a_slot_posts_the_slot_and_a_taken_slot_is_explained()
    {
        var start = _factory.Time.GetUtcNow().AddDays(1);
        _factory.Api.On(HttpMethod.Get, "api/v1/consultations/availability", _ => FakeOnyxApi.Json(
            new AvailabilityResponse(true, "Asia/Riyadh", 30, [new AvailableSlotResponse(start, start.AddMinutes(30), start.ToOffset(TimeSpan.FromHours(3)))])));
        _factory.Api.On(HttpMethod.Post, "api/v1/consultations", _ => FakeOnyxApi.Problem(HttpStatusCode.Conflict, "Booking.SlotUnavailable"));

        var html = await _browser.GetHtmlAsync("/en/book-a-consultation");
        Assert.Contains("data-action=\"booking\"", html, StringComparison.Ordinal);
        Assert.Contains("Asia/Riyadh", html, StringComparison.Ordinal);

        using var response = await _browser.PostFormAsync("/en/book-a-consultation", new Dictionary<string, string>
        {
            ["RequestOnly"] = "false", ["StartUtc"] = start.ToString("O"), ["Name"] = "Sample Visitor", ["Email"] = "visitor@example.com", ["ContactConsent"] = "true",
        }, BrowserExtensions.AntiforgeryToken(html));

        Assert.Contains("That time has just been booked", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        var sent = JsonSerializer.Deserialize<BookConsultationRequest>(Assert.Single(_factory.Api.RequestsTo("api/v1/consultations")).Body!, Json)!;
        Assert.Equal(start, sent.StartUtc);
        Assert.Empty(_factory.Api.RequestsTo("api/v1/inquiries"));
    }

    [Fact]
    public async Task The_manage_page_is_private_and_sends_the_token_only_in_request_bodies()
    {
        const string ManageToken = "tok_0123456789abcdefghijklmnopqrstuvwxyzABCDE";
        var start = _factory.Time.GetUtcNow().AddDays(2);
        _factory.Api.On(HttpMethod.Post, "api/v1/consultations/manage", _ => FakeOnyxApi.Json(
            new ManagedBookingResponse(start, start.AddMinutes(30), start.ToOffset(TimeSpan.FromHours(3)), "Asia/Riyadh", "Confirmed", "en", true)));
        _factory.Api.On(HttpMethod.Get, "api/v1/consultations/availability", _ => FakeOnyxApi.Json(new AvailabilityResponse(true, "Asia/Riyadh", 30, [])));

        using var response = await _browser.GetPathAsync($"/en/manage-booking?token={ManageToken}");
        var html = await response.Content.ReadAsStringAsync(Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Contains("noindex", html, StringComparison.Ordinal);
        var call = Assert.Single(_factory.Api.RequestsTo("api/v1/consultations/manage"));
        Assert.Empty(call.Query);
        Assert.Contains(ManageToken, call.Body, StringComparison.Ordinal);
        Assert.Contains("Cancel the consultation", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Attribution_cookies_exist_only_with_analytics_consent()
    {
        var consentId = Guid.NewGuid();

        using var withConsent = await GetWithCookieAsync("/en/contact?utm_source=launch", ConsentState.CookieName + "=" + ConsentState.Format("1", true, false, consentId));
        var first = Assert.Single(withConsent.SetCookies(AttributionCookies.FirstTouchCookie));
        Assert.Contains("httponly", first, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", first, StringComparison.OrdinalIgnoreCase);
        Assert.Single(withConsent.SetCookies(AttributionCookies.LastTouchCookie));

        using var withoutConsent = await GetWithCookieAsync("/en/contact?utm_source=launch", ConsentState.CookieName + "=" + ConsentState.Format("1", false, false, consentId));
        Assert.Empty(withoutConsent.SetCookies(AttributionCookies.LastTouchCookie));

        // A previously stored first touch is removed once analytics consent is withdrawn.
        using var withdrawn = await GetWithCookieAsync(
            "/en/contact", $"{ConsentState.CookieName}={ConsentState.Format("1", false, false, consentId)}; {AttributionCookies.FirstTouchCookie}=abc");
        Assert.Contains("expires=Thu, 01 Jan 1970", Assert.Single(withdrawn.SetCookies(AttributionCookies.FirstTouchCookie)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Conversion_calls_to_action_link_to_their_form_with_the_page_context()
    {
        var routes = new LocalizedRouteTable();
        foreach (var route in LeadPages.Routes)
        {
            routes.Register(route);
        }

        var ctaId = Guid.CreateVersion7();
        var page = new PublicContentResponse(ServiceId, "Service", "en", "Service", "", [], [], null!, [], [], null, null, null, null);

        Assert.Equal($"/en/start-a-project?service={ServiceId:D}&cta={ctaId:D}", CtaLinks.Href(routes, "en", new PublicCtaResponse("StartProject", "Start", null, null, ctaId), page));
        Assert.Equal($"/en/book-a-consultation?service={ServiceId:D}", CtaLinks.Href(routes, "en", new PublicCtaResponse("BookConsultation", "Book", null, null), page));
        Assert.Equal("/ar/" + Uri.EscapeDataString("تواصل-معنا"), CtaLinks.Href(routes, "ar", new PublicCtaResponse("Contact", "تواصل", null, null), null));
        Assert.Null(CtaLinks.Href(routes, "en", new PublicCtaResponse("AnalyzeWebsite", "Analyze", null, null, ctaId), page));
        Assert.Equal("https://example.com/x", CtaLinks.Href(routes, "en", new PublicCtaResponse("Url", "Go", null, "https://example.com/x"), page));

        var project = page with { Type = "Project" };
        Assert.Equal($"/en/contact?project={ServiceId:D}", CtaLinks.Href(routes, "en", new PublicCtaResponse("Contact", "Similar", null, null), project));
    }

    [Fact]
    public void Conversion_segments_are_reserved_from_cms_pages() =>
        Assert.All(LeadRouteSegments.All, segment => Assert.Contains(segment, ContentRouteSegments.Reserved));

    private async Task<HttpResponseMessage> GetWithCookieAsync(string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.Add("Cookie", cookie);
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        return await client.SendAsync(request, Token);
    }
}
