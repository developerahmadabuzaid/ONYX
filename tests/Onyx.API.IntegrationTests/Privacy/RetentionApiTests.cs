using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Privacy;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Leads;
using Onyx.Contracts.Privacy;
using Onyx.Domain.Auditing;
using Onyx.Domain.Privacy;
using Onyx.Infrastructure.BackgroundJobs;
using Onyx.Infrastructure.Email;

namespace Onyx.API.IntegrationTests.Privacy;

/// <summary>Retention: nothing happens until an enabled policy with a period exists; exact selection; bounded, idempotent runs.</summary>
public sealed class RetentionApiTests(PostgresFixture fixture) : PrivacyApiTestBase(fixture)
{
    [Fact]
    public async Task Nothing_is_processed_without_an_enabled_policy_with_a_period()
    {
        Fixture.RequireDatabase();
        var officer = await OfficerAsync();
        await SubmitOkAsync(InquiryTypes.Contact, Form(email: Subject));

        var policies = await ReadAsync<List<RetentionPolicyResponse>>(await officer.GetAsync("/api/v1/admin/retention-policies", Token));
        Assert.Equal(Enum.GetValues<RetentionCategory>().Length, policies.Count);
        Assert.All(policies, policy => Assert.False(policy.IsActive));
        Assert.All(policies, policy => Assert.Null(policy.PeriodDays));

        // Saving without a period cannot enable; a disabled policy with a period stays inactive.
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(officer.PutAsJsonAsync(
            "/api/v1/admin/retention-policies/LeadsAndInquiries", new SaveRetentionPolicyRequest("Anonymize", null, true, null), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(officer.PutAsJsonAsync(
            "/api/v1/admin/retention-policies/EmailMessages", new SaveRetentionPolicyRequest("Anonymize", 30, false, null), Token)));
        await ReadAsync<RetentionPolicyResponse>(await officer.PutAsJsonAsync(
            "/api/v1/admin/retention-policies/LeadsAndInquiries", new SaveRetentionPolicyRequest("Delete", 1, false, null), Token));

        Time.Advance(TimeSpan.FromDays(400));
        await using var worker = CreateWorker();
        var outcome = await worker.InScopeAsync(services => new RetentionEnforcementJob(services.GetRequiredService<IRetentionService>()).ExecuteAsync(Token));

        Assert.True(outcome.WasSkipped);
        Assert.Equal(1, await CountAsync(db => db.Leads));
        Assert.Equal(1, await CountAsync(db => db.Inquiries));
        Assert.Equal(0, await CountAsync(db => db.AuditLogs.Where(log => log.Action == AuditAction.RetentionRunCompleted)));
    }

    [Fact]
    public async Task Lead_anonymization_selects_exactly_the_due_leads_and_rerunning_is_harmless()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var officer = await OfficerAsync();
        await SubmitOkAsync(InquiryTypes.Contact, Form(email: "old@example.com", company: "Old Co"));
        await BookFirstSlotAsync(owner, Form(email: "booked@example.com", token: Solved("booking")));
        await ReadAsync<RetentionPolicyResponse>(await officer.PutAsJsonAsync(
            "/api/v1/admin/retention-policies/LeadsAndInquiries", new SaveRetentionPolicyRequest("Anonymize", 30, true, null), Token));

        // Thirty-one days later a new lead arrives; both earlier leads (the booking is now in the past) are due.
        Time.Advance(TimeSpan.FromDays(31));
        await SubmitOkAsync(InquiryTypes.Contact, Form(email: "new@example.com"));

        var reviewer = await SignInWithAsync("reviewer", Onyx.Contracts.Authorization.Permissions.Privacy.Manage);
        var preview = await ReadAsync<RetentionPreviewResponse>(await reviewer.GetAsync("/api/v1/admin/retention-policies/LeadsAndInquiries/preview?action=Anonymize&periodDays=30", Token));
        Assert.Equal(2, preview.EligibleCount);
        Assert.Equal(2, await CountAsync(db => db.Leads.Where(lead => lead.ErasedAtUtc == null && lead.LastInquiryAtUtc < Time.GetUtcNow().AddDays(-30))));

        await using var worker = CreateWorker();
        var first = await worker.InScopeAsync(services => services.GetRequiredService<IRetentionService>().EnforceAsync(Token));
        var second = await worker.InScopeAsync(services => services.GetRequiredService<IRetentionService>().EnforceAsync(Token));

        Assert.Equal(2, Assert.Single(first).Processed);
        Assert.Equal(0, Assert.Single(second).Processed);
        await using var db = await DbAsync();
        var leads = await db.Leads.ToListAsync(Token);
        Assert.Equal(2, leads.Count(lead => lead.ErasedAtUtc != null));
        Assert.Contains(leads, lead => lead.NormalizedEmail == "new@example.com" && lead.ErasedAtUtc == null);
        Assert.DoesNotContain(leads, lead => lead.Company == "Old Co");
        var audit = await db.AuditLogs.Where(log => log.Action == AuditAction.RetentionRunCompleted).Select(log => log.Changes).ToListAsync(Token);
        Assert.Equal(2, audit.Count);
        Assert.All(audit, changes => Assert.DoesNotContain("@example.com", changes!, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Leads_with_an_upcoming_booking_are_kept_and_delete_removes_the_whole_lead_graph()
    {
        Fixture.RequireDatabase();
        var officer = await OfficerAsync();
        await SubmitOkAsync(InquiryTypes.Contact, Form(email: "old@example.com"));
        await ReadAsync<RetentionPolicyResponse>(await officer.PutAsJsonAsync(
            "/api/v1/admin/retention-policies/LeadsAndInquiries", new SaveRetentionPolicyRequest("Delete", 1, true, null), Token));
        Time.Advance(TimeSpan.FromDays(2));

        // An old lead who has just booked (the booking is still ahead) must not be deleted.
        var staff = await SignInWithAsync("staff", Onyx.Contracts.Authorization.Permissions.Availability.Manage);
        await OpenBookingAsync(staff);
        var lastSlot = (await ReadAsync<AvailabilityResponse>(await Client.GetAsync("/api/v1/consultations/availability", Token))).Slots[^1];
        await ReadAsync<BookingConfirmedResponse>(await Client.PostAsJsonAsync(
            "/api/v1/consultations", new BookConsultationRequest(lastSlot.StartUtc, Form(email: "old@example.com", token: Solved("booking"))), Token));
        await SubmitOkAsync(InquiryTypes.Contact, Form(email: "gone@example.com"));
        Time.Advance(TimeSpan.FromDays(1).Add(TimeSpan.FromMinutes(1)));

        await using var worker = CreateWorker();
        await worker.InScopeAsync(services => services.GetRequiredService<IRetentionService>().EnforceAsync(Token));

        await using var db = await DbAsync();
        Assert.Equal(["old@example.com"], await db.Leads.Select(lead => lead.NormalizedEmail).ToListAsync(Token));
        Assert.False(await db.Inquiries.AnyAsync(inquiry => inquiry.NormalizedEmail == "gone@example.com", Token));
        Assert.False(await db.ConsentRecords.AnyAsync(consent => !db.Leads.Any(lead => lead.Id == consent.LeadId), Token));
        Assert.False(await db.LeadActivities.AnyAsync(activity => !db.Leads.Any(lead => lead.Id == activity.LeadId), Token));
    }

    [Fact]
    public async Task Batches_are_bounded_and_set_based_categories_touch_only_their_eligible_records()
    {
        Fixture.RequireDatabase();
        var officer = await OfficerAsync();
        foreach (var email in new[] { "a@example.com", "b@example.com", "c@example.com" })
        {
            await SubmitOkAsync(InquiryTypes.Contact, Form(email: email));
        }

        await using (var worker = CreateWorker())
        {
            await worker.Get<OutboxProcessor>().ProcessBatchAsync(Token);
        }

        await using (var db = await DbAsync())
        {
            // Two of the confirmations were "sent"; one stays pending and must never be deleted by retention.
            var visitorEmails = await db.EmailMessages.Where(message => message.TemplateKey == "leads.inquiry-received").OrderBy(message => message.ToAddress).Select(message => message.Id).ToListAsync(Token);
            await db.EmailMessages.Where(message => message.Id != visitorEmails[0])
                .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.Status, EmailMessageStatus.Sent), Token);
        }

        await ReadAsync<RetentionPolicyResponse>(await officer.PutAsJsonAsync(
            "/api/v1/admin/retention-policies/EmailMessages", new SaveRetentionPolicyRequest("Delete", 10, true, null), Token));
        await ReadAsync<RetentionPolicyResponse>(await officer.PutAsJsonAsync(
            "/api/v1/admin/retention-policies/AuditLogs", new SaveRetentionPolicyRequest("Anonymize", 10, true, null), Token));
        var sentBefore = await CountAsync(db => db.EmailMessages.Where(message => message.Status == EmailMessageStatus.Sent));
        Time.Advance(TimeSpan.FromDays(11));

        await using (var worker = CreateWorker(new Dictionary<string, string?> { ["Privacy:RetentionBatchSize"] = "1", ["Privacy:RetentionMaxBatchesPerRun"] = "2" }))
        {
            // Each run processes at most 2 × 1 record per category and says when more remain; later runs continue.
            var first = (await worker.InScopeAsync(services => services.GetRequiredService<IRetentionService>().EnforceAsync(Token)))
                .Single(summary => summary.Category == "EmailMessages");
            Assert.Equal(2, first.Processed);
            Assert.True(first.MoreRemaining);

            var deleted = first.Processed;
            for (var run = 0; run < 100; run++)
            {
                var summaries = await worker.InScopeAsync(services => services.GetRequiredService<IRetentionService>().EnforceAsync(Token));
                deleted += summaries.Single(summary => summary.Category == "EmailMessages").Processed;
                if (summaries.All(summary => summary.Processed == 0))
                {
                    break;
                }
            }

            Assert.Equal(sentBefore, deleted);
        }

        await using var check = await DbAsync();
        Assert.Equal(1, await check.EmailMessages.CountAsync(Token));
        Assert.Equal(EmailMessageStatus.Pending, (await check.EmailMessages.SingleAsync(Token)).Status);

        // Audit entries older than the period lost user names and IPs; the entries themselves remain.
        var cutoff = Time.GetUtcNow().AddDays(-10);
        Assert.False(await check.AuditLogs.AnyAsync(log => log.OccurredAtUtc < cutoff && (log.IpAddress != null || log.UserName != null), Token));
        Assert.True(await check.AuditLogs.AnyAsync(log => log.OccurredAtUtc < cutoff, Token));
    }
}
