using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.Leads;
using Onyx.API.IntegrationTests.Worker;
using Onyx.Application.Email;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Leads;
using Onyx.Contracts.Privacy;

namespace Onyx.API.IntegrationTests.Privacy;

/// <summary>Privacy tests: realistic Stage 8 data created through the public API, and a privacy officer holding Privacy.Manage.</summary>
public abstract class PrivacyApiTestBase(PostgresFixture fixture) : LeadsApiTestBase(fixture)
{
    protected const string Subject = "subject@example.com";
    protected const string Other = "other@example.com";

    protected FakeEmailSender Sender { get; } = new();

    /// <summary>Privacy.Manage is 2FA-privileged; the sign-in helper enrols the authenticator.</summary>
    protected Task<HttpClient> OfficerAsync(string name = "officer") => SignInWithAsync(name, Permissions.Privacy.Manage);

    protected static InquiryFormDto Arabic(string email, string token, string? company = null, bool marketing = false, AttributionDto? attribution = null) =>
        new(
            "ar", "سارة العتيبي", email, "+966 11 000 0000", company, "SA", "نحتاج إلى أتمتة الموافقات الداخلية وربطها بالنظام المالي.", 1,
            new Dictionary<string, IReadOnlyList<string>> { ["pillars"] = ["automation", "ai"] }, null, null, null, true, marketing, token, null, attribution);

    protected async Task SubmitOkAsync(string type, InquiryFormDto form) =>
        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(type, form)));

    protected async Task<BookingConfirmedResponse> BookFirstSlotAsync(HttpClient admin, InquiryFormDto form)
    {
        await OpenBookingAsync(admin);
        var slot = (await ReadAsync<AvailabilityResponse>(await Client.GetAsync("/api/v1/consultations/availability", Token))).Slots[0];
        return await ReadAsync<BookingConfirmedResponse>(await Client.PostAsJsonAsync("/api/v1/consultations", new BookConsultationRequest(slot.StartUtc, form), Token));
    }

    protected async Task<DataSubjectRequestResponse> VerifiedRequestAsync(HttpClient officer, string type, string email)
    {
        var created = await ReadAsync<DataSubjectRequestResponse>(await officer.PostAsJsonAsync(
            "/api/v1/admin/data-subject-requests", new CreateDataSubjectRequestRequest(type, email, "Received by email from the subject."), Token));
        return await ReadAsync<DataSubjectRequestResponse>(await officer.PostAsJsonAsync(
            $"/api/v1/admin/data-subject-requests/{created.Id}/verify", new VerifyDataSubjectRequestRequest("Replied from the same address; identity confirmed.", created.Version), Token));
    }

    protected WorkerTestHost CreateWorker(IReadOnlyDictionary<string, string?>? extra = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Leads:DefaultRecipients:0"] = Recipient,
            ["IdentitySecurity:AdminPortalBaseUrl"] = OnyxApiFactory.AdminPortalBaseUrl,
            ["Site:PublicBaseUrl"] = $"https://{SiteHost}",
        };
        foreach (var (key, value) in extra ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        return WorkerTestHost.Create(
            Fixture.ApplicationDatabase!.ConnectionString,
            "worker-privacy",
            Time,
            services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(Sender);
            },
            settings);
    }
}
