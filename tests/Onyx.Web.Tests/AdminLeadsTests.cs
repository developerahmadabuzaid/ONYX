using System.Net;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>Admin lead screens: permission-filtered menu and controls, the origin panel, and API-enforced actions.</summary>
public sealed class AdminLeadsTests : IAsyncLifetime
{
    private static readonly Guid LeadId = Guid.CreateVersion7();
    private static readonly Guid ConsentId = Guid.CreateVersion7();

    private readonly OnyxWebFactory _factory = new();
    private HttpClient _browser = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
    public async Task Lead_screens_appear_only_with_their_permissions()
    {
        await SignInAsync(Permissions.Leads.View);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/leads", _ => FakeOnyxApi.Json(new PagedResult<LeadSummaryResponse>([Summary()], 1, 25, 1)));

        var html = await _browser.GetHtmlAsync("/admin/leads");

        Assert.Contains("href=\"/admin/leads\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/bookings\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/availability\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/admin/leads/export", html, StringComparison.Ordinal);
        Assert.Contains("Sample Visitor", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lead_detail_shows_the_origin_trail_and_hides_actions_the_user_cannot_take()
    {
        await SignInAsync(Permissions.Leads.View);
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/leads/{LeadId:D}", _ => FakeOnyxApi.Json(Detail()));

        var html = await _browser.GetHtmlAsync($"/admin/leads/{LeadId:D}");

        Assert.Contains("utm_source=newsletter", html, StringComparison.Ordinal);
        Assert.Contains("/en/contact", html, StringComparison.Ordinal);
        Assert.Contains("booked a consultation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("withdraw-marketing", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"/admin/leads/{LeadId:D}/stage", html, StringComparison.Ordinal);
        Assert.Empty(_factory.Api.RequestsTo("api/v1/admin/leads/assignees"));
    }

    [Fact]
    public async Task Stage_changes_go_to_the_api_with_the_version_and_api_refusals_are_shown()
    {
        await SignInAsync(Permissions.Leads.View, Permissions.Leads.Manage);
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/leads/{LeadId:D}", _ => FakeOnyxApi.Json(Detail()));
        _factory.Api.On(HttpMethod.Put, $"api/v1/admin/leads/{LeadId:D}/stage", _ => FakeOnyxApi.Problem(HttpStatusCode.Conflict, "Concurrency.Conflict"));

        using var response = await _browser.SubmitFormAsync($"/admin/leads/{LeadId:D}", $"/admin/leads/{LeadId:D}/stage", new Dictionary<string, string>
        {
            ["stage"] = "Qualified",
            ["version"] = "7",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var call = Assert.Single(_factory.Api.RequestsTo($"api/v1/admin/leads/{LeadId:D}/stage"));
        Assert.Contains("\"stage\":\"Qualified\"", call.Body, StringComparison.Ordinal);
        Assert.Contains("\"version\":7", call.Body, StringComparison.Ordinal);
        Assert.Contains("Concurrency.Conflict", await _browser.GetHtmlAsync($"/admin/leads/{LeadId:D}"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_availability_screen_states_that_booking_is_closed_until_configured()
    {
        await SignInAsync(Permissions.Availability.Manage);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/availability", _ => FakeOnyxApi.Json(
            new BookingSettingsResponse(false, false, "Asia/Riyadh", 0, 0, 0, 0, 0, [], [])));

        var html = await _browser.GetHtmlAsync("/admin/availability");

        Assert.Contains("Online booking is closed.", html, StringComparison.Ordinal);
        Assert.Contains("No weekly hours yet.", html, StringComparison.Ordinal);
        Assert.Contains("value=\"Asia/Riyadh\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_sign_in_renders_the_challenge_and_forwards_its_token_to_the_api()
    {
        AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", Permissions.Leads.View));

        using var page = await _browser.GetPathAsync("/admin/account/login");
        var html = await page.Content.ReadAsStringAsync(Token);
        Assert.Contains("data-action=\"admin-login\"", html, StringComparison.Ordinal);
        Assert.Contains("https://challenges.cloudflare.com", page.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);

        using var login = await _browser.PostFormAsync("/admin/account/login", new Dictionary<string, string>
        {
            ["Email"] = AdminApiStubs.Email,
            ["Password"] = AdminApiStubs.Password,
            ["cf-turnstile-response"] = "solved-login-token",
        }, BrowserExtensions.AntiforgeryToken(html));

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("\"captchaToken\":\"solved-login-token\"", Assert.Single(_factory.Api.RequestsTo("api/v1/auth/login")).Body, StringComparison.Ordinal);
    }

    private async Task SignInAsync(params string[] permissions)
    {
        AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", permissions));
        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    private LeadSummaryResponse Summary() =>
        new(LeadId, "Sample Visitor", null, "New", "Contact", "Normal", 30, null, null, 1, "en", _factory.Time.GetUtcNow(), _factory.Time.GetUtcNow(), 7);

    private LeadDetailResponse Detail()
    {
        var now = _factory.Time.GetUtcNow();
        var origin = new OriginResponse(
            null, new TouchPointDto("/en/services", null, "newsletter", null, null, null, null, now), "/en/contact", "en", null, "desktop", null, null, null, false);
        var inquiry = new InquiryResponse(
            Guid.CreateVersion7(), LeadId, "Consultation", "en", "Sample Visitor", "visitor@example.com", null, null, null, "Hello", 1,
            new Dictionary<string, IReadOnlyList<string>>(), null, null, null, now, now, origin,
            new ConsentResponse(ConsentId, true, true, true, "1", now, null));
        return new LeadDetailResponse(Summary(), null, ["booked-consultation"], null, [inquiry], [], [], [new LeadBookingResponse(Guid.CreateVersion7(), now, now.AddMinutes(30), "Asia/Riyadh", "Confirmed")]);
    }
}
