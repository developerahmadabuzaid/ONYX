using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Leads;
using Onyx.Domain.Leads;
using Onyx.Infrastructure.Persistence;

namespace Onyx.API.IntegrationTests.Leads;

/// <summary>Public form submissions: system of record, server-side Turnstile, honeypot, validation, consent-aware attribution.</summary>
public sealed class InquiryApiTests(PostgresFixture fixture) : LeadsApiTestBase(fixture)
{
    [Fact]
    public async Task Submission_stores_inquiry_consent_lead_and_outbox_event_in_one_transaction()
    {
        Fixture.RequireDatabase();

        using var response = await SubmitAsync(InquiryTypes.Contact, Form(marketingConsent: true));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<InquiryAcceptedResponse>(Token);
        Assert.StartsWith("ONX-", accepted!.Reference, StringComparison.Ordinal);

        await using var db = await DbAsync();
        var inquiry = await db.Inquiries.SingleAsync(Token);
        Assert.Equal(InquiryType.Contact, inquiry.Type);
        Assert.Equal("visitor@example.com", inquiry.NormalizedEmail);
        var consent = await db.ConsentRecords.SingleAsync(Token);
        Assert.True(consent.ContactConsent);
        Assert.True(consent.MarketingConsent);
        Assert.Equal("2026-10", consent.ConsentVersion);
        var lead = await db.Leads.SingleAsync(Token);
        Assert.Equal(inquiry.LeadId, lead.Id);
        Assert.Equal(LeadStage.New, lead.Stage);
        Assert.Equal(1, lead.InquiryCount);
        Assert.Single(await db.OutboxMessages.Where(message => message.Type.Contains(nameof(InquirySubmitted))).ToListAsync(Token));
        Assert.Single(await db.LeadActivities.ToListAsync(Token));

        // Emails are queued by the Worker, never inside the visitor request.
        Assert.Empty(await db.EmailMessages.ToListAsync(Token));

        // The real verifier sent the secret and the token to the provider (the test server has no client IP to forward).
        var sent = Assert.Single(Turnstile.Requests);
        Assert.Equal("test-secret", sent["secret"]);
        Assert.StartsWith("ok:inquiry:", sent["response"], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fail")]
    [InlineData("ok:booking:onyx.test")]
    [InlineData("ok:inquiry:evil.example")]
    [InlineData("stale:inquiry:onyx.test")]
    [InlineData("")]
    public async Task Turnstile_rejection_stores_nothing(string token)
    {
        Fixture.RequireDatabase();

        using var response = await SubmitAsync(InquiryTypes.Contact, Form(token: token));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Captcha.Failed", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(db => db.Inquiries));
        Assert.Equal(0, await CountAsync(db => db.Leads));
        Assert.Equal(0, await CountAsync(db => db.OutboxMessages));
    }

    [Fact]
    public async Task A_turnstile_token_is_accepted_once_only()
    {
        Fixture.RequireDatabase();
        var token = Solved();

        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form(token: token))));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form(token: token))));
        Assert.Equal(1, await CountAsync(db => db.Inquiries));
    }

    [Fact]
    public async Task Honeypot_submissions_look_accepted_but_store_nothing_and_skip_turnstile()
    {
        Fixture.RequireDatabase();

        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form(honeypot: "https://spam.example"))));

        Assert.Equal(0, await CountAsync(db => db.Inquiries));
        Assert.Equal(0, await CountAsync(db => db.Leads));
        Assert.Empty(Turnstile.Requests);
    }

    [Fact]
    public async Task Invalid_submissions_are_refused_before_turnstile()
    {
        Fixture.RequireDatabase();

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form(contactConsent: false))));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form(email: "not-an-email"))));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form(message: null))));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form(language: "fr"))));

        // Bookings use the consultations resource; tool and advisor sources are not open yet.
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(SubmitAsync(InquiryTypes.Consultation, Form())));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(SubmitAsync("ToolFollowUp", Form())));

        // A service inquiry must reference live service content.
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(SubmitAsync(InquiryTypes.ServiceInquiry, Form(serviceId: Guid.NewGuid()))));

        Assert.Equal(0, await CountAsync(db => db.Inquiries));
        Assert.Empty(Turnstile.Requests);
    }

    [Fact]
    public async Task Attribution_keeps_first_touch_and_client_id_only_with_analytics_consent()
    {
        Fixture.RequireDatabase();
        var first = new TouchPointDto("/en/services", "https://www.google.com/search?q=secret", "google", "cpc", "launch", null, null, Time.GetUtcNow().AddDays(-3));
        var last = new TouchPointDto("/en/contact", null, "newsletter", "email", "october", null, null, Time.GetUtcNow());

        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(InquiryTypes.Contact,
            Form(email: "with@example.com", attribution: new AttributionDto(first, last, "/en/contact", "SA", "Mobile", "123.456", true)))));
        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(InquiryTypes.Contact,
            Form(email: "without@example.com", attribution: new AttributionDto(first, last, "/en/contact", "SA", "mobile", "123.456", false)))));

        await using var db = await DbAsync();
        var withConsent = (await db.Inquiries.SingleAsync(inquiry => inquiry.NormalizedEmail == "with@example.com", Token)).Attribution;
        Assert.True(withConsent.AnalyticsConsent);
        Assert.Equal("google", withConsent.FirstTouch!.UtmSource);
        Assert.Equal("https://www.google.com", withConsent.FirstTouch.Referrer);
        Assert.Equal("123.456", withConsent.AnalyticsClientId);
        Assert.Equal("mobile", withConsent.DeviceClass);
        Assert.Equal("SA", withConsent.Market);

        var withoutConsent = (await db.Inquiries.SingleAsync(inquiry => inquiry.NormalizedEmail == "without@example.com", Token)).Attribution;
        Assert.False(withoutConsent.AnalyticsConsent);
        Assert.Null(withoutConsent.FirstTouch);
        Assert.Null(withoutConsent.AnalyticsClientId);
        Assert.Equal("newsletter", withoutConsent.LastTouch!.UtmSource);
        Assert.Equal("/en/contact", withoutConsent.SubmissionPath);
    }

    [Fact]
    public async Task Repeat_inquiries_from_the_same_email_attach_to_the_open_lead()
    {
        Fixture.RequireDatabase();

        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form(email: "Repeat@Example.com"))));
        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(InquiryTypes.StartProject, Form(email: "repeat@example.com", company: "Sample Co"))));

        await using var db = await DbAsync();
        var lead = await db.Leads.SingleAsync(Token);
        Assert.Equal(2, lead.InquiryCount);
        Assert.Equal("Sample Co", lead.Company);
        Assert.Contains("repeat-inquiry", lead.ScoreReasons);
        Assert.Contains("project-or-consultation-request", lead.ScoreReasons);
        Assert.Equal(2, await db.Inquiries.CountAsync(inquiry => inquiry.LeadId == lead.Id, Token));
    }

    [Fact]
    public async Task Public_form_endpoints_need_no_authentication_and_admin_resources_do()
    {
        Fixture.RequireDatabase();

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(Client.GetAsync("/api/v1/consultations/availability", Token)));
        foreach (var path in new[] { "/api/v1/admin/leads", "/api/v1/admin/inquiries", "/api/v1/admin/bookings", "/api/v1/admin/availability", "/api/v1/admin/leads/export" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync(path, Token)));
        }
    }
}
