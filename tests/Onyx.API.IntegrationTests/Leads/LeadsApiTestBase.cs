using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Content;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Leads;

namespace Onyx.API.IntegrationTests.Leads;

/// <summary>
/// Real API + PostgreSQL with the real Turnstile adapter talking to <see cref="FakeTurnstile"/>. The site host is
/// <see cref="SiteHost"/>; tokens solved for another host or action are refused.
/// </summary>
public abstract class LeadsApiTestBase(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    protected const string SiteHost = "onyx.test";
    protected const string Recipient = "sales@onyx.test";

    private FakeTurnstile? _turnstile;

    protected FakeTurnstile Turnstile => _turnstile ??= new FakeTurnstile(Time);

    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>(base.Settings)
    {
        ["Turnstile:SecretKey"] = "test-secret",
        ["Site:PublicBaseUrl"] = $"https://{SiteHost}",
        ["Leads:DefaultRecipients:0"] = Recipient,
        ["Leads:ConsentVersion"] = "2026-10",
    };

    protected override void ConfigureTestServices(IServiceCollection services) =>
        services.AddHttpClient("ICaptchaVerifier").ConfigurePrimaryHttpMessageHandler(() => Turnstile);

    protected const string AdminHost = "admin.onyx.test";

    protected override string? LoginCaptchaToken() => Solved("admin-login", AdminHost);

    /// <summary>A unique solved token (tokens are single use).</summary>
    protected static string Solved(string action = "inquiry", string host = SiteHost) => $"ok:{action}:{host}:{Guid.NewGuid():N}";

    protected static InquiryFormDto Form(
        string email = "visitor@example.com",
        string? token = null,
        string language = "en",
        string? message = "We need help automating our approvals.",
        bool contactConsent = true,
        bool marketingConsent = false,
        string? honeypot = null,
        AttributionDto? attribution = null,
        Guid? serviceId = null,
        string? phone = null,
        string? company = null) =>
        new(
            language, "Sample Visitor", email, phone, company, null, message, 1,
            new Dictionary<string, IReadOnlyList<string>> { ["pillars"] = ["automation"] }, serviceId, null, null, contactConsent, marketingConsent,
            token ?? Solved(), honeypot, attribution);

    protected Task<HttpResponseMessage> SubmitAsync(string type, InquiryFormDto form) =>
        Client.PostAsJsonAsync("/api/v1/inquiries", new SubmitInquiryRequest(type, form), Token);

    /// <summary>Opens online booking every day 09:00–17:00 (Asia/Riyadh), 30-minute slots, no notice, 7 days ahead.</summary>
    protected async Task OpenBookingAsync(HttpClient admin)
    {
        await ReadAsync<BookingSettingsResponse>(await admin.PutAsJsonAsync(
            "/api/v1/admin/availability/settings", new SaveBookingSettingsRequest(true, "Asia/Riyadh", 30, 0, 0, 7, null), Token));
        foreach (var day in Enum.GetNames<DayOfWeek>())
        {
            await ReadAsync<BookingSettingsResponse>(await admin.PostAsJsonAsync(
                "/api/v1/admin/availability/rules", new SaveAvailabilityRuleRequest(day, "09:00", "17:00"), Token));
        }
    }

    protected async Task<int> CountAsync<T>(Func<Onyx.Infrastructure.Persistence.ApplicationDbContext, IQueryable<T>> query)
    {
        await using var db = await DbAsync();
        return await query(db).CountAsync(Token);
    }
}
