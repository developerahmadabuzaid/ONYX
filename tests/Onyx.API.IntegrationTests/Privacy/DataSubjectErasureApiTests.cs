using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;
using Onyx.Contracts.Privacy;
using Onyx.Domain.Auditing;
using Onyx.Domain.Booking;
using Onyx.Domain.Leads;
using Onyx.Domain.Privacy;
using Onyx.Infrastructure.BackgroundJobs;

namespace Onyx.API.IntegrationTests.Privacy;

/// <summary>Data-subject erasure: exact per-category result, retained records without personal data, idempotent, guarded, audited.</summary>
public sealed class DataSubjectErasureApiTests(PostgresFixture fixture) : PrivacyApiTestBase(fixture)
{
    [Fact]
    public async Task Erasure_removes_the_subjects_personal_data_everywhere_and_leaves_everyone_else_untouched()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var officer = await OfficerAsync();

        await SubmitOkAsync(InquiryTypes.Contact, Arabic(Subject, Solved(), company: "شركة النخبة", marketing: true,
            attribution: new AttributionDto(null, new TouchPointDto("/ar", "https://ref.example/x", "google", "cpc", "launch", "ad-1", "term", null), "/ar", "SA", "mobile", "111.222", true)));
        await BookFirstSlotAsync(owner, Form(email: Subject, token: Solved("booking")));
        await SubmitOkAsync(InquiryTypes.Contact, Form(email: Other, company: "Other Co", marketingConsent: true));
        var subjectAccount = await CreateUserAsync(Subject, StrongPassword, SystemRoles.Sales);
        await SignInAsync(Subject, StrongPassword);

        Guid leadId;
        await using (var db = await DbAsync())
        {
            leadId = await db.Leads.Where(lead => lead.NormalizedEmail == Subject).Select(lead => lead.Id).SingleAsync(Token);
        }

        await ReadAsync<LeadNoteResponse>(await owner.PostAsJsonAsync($"/api/v1/admin/leads/{leadId}/notes", new AddLeadNoteRequest("Prefers WhatsApp calls in the evening."), Token));
        await using (var worker = CreateWorker())
        {
            await worker.Get<OutboxProcessor>().ProcessBatchAsync(Token);
        }

        var earlierExport = await VerifiedRequestAsync(officer, "Export", Subject);
        var request = await VerifiedRequestAsync(officer, "Erasure", Subject);

        // The confirmation must repeat the stored address; nothing changes otherwise.
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(officer.PostAsJsonAsync(
            $"/api/v1/admin/data-subject-requests/{request.Id}/erase", new ConfirmErasureRequest(Other), Token)));
        Assert.Equal(1, await CountAsync(db => db.LeadNotes));

        var erased = await ReadAsync<DataSubjectRequestResponse>(await officer.PostAsJsonAsync(
            $"/api/v1/admin/data-subject-requests/{request.Id}/erase", new ConfirmErasureRequest(" Subject@Example.com "), Token));
        Assert.Equal("Completed", erased.Status);
        Assert.Null(erased.SubjectEmail);

        await using (var db = await DbAsync())
        {
            var inquiry = await db.Inquiries.SingleAsync(item => item.LeadId == leadId && item.LanguageCode == "ar", Token);
            Assert.Equal(ErasedValues.Name, inquiry.Name);
            Assert.EndsWith("@erased.invalid", inquiry.Email, StringComparison.Ordinal);
            Assert.Null(inquiry.Phone);
            Assert.Null(inquiry.Company);
            Assert.Null(inquiry.Country);
            Assert.Null(inquiry.Message);
            Assert.Empty(inquiry.Answers.Values);
            Assert.Null(inquiry.Attribution.AnalyticsClientId);
            Assert.Null(inquiry.Attribution.LastTouch!.Referrer);
            Assert.Null(inquiry.Attribution.LastTouch.UtmContent);
            Assert.Equal("google", inquiry.Attribution.LastTouch.UtmSource);
            Assert.NotNull(inquiry.ErasedAtUtc);
            Assert.Equal(InquiryType.Contact, inquiry.Type);

            var lead = await db.Leads.SingleAsync(item => item.Id == leadId, Token);
            Assert.Equal(ErasedValues.Name, lead.DisplayName);
            Assert.Null(lead.Company);
            Assert.NotEqual(Subject, lead.NormalizedEmail);
            Assert.Contains(await db.LeadActivities.Where(item => item.LeadId == leadId).Select(item => item.Kind).ToListAsync(Token), kind => kind == LeadActivityKind.Erased);
            Assert.Equal(0, await db.LeadNotes.CountAsync(note => note.LeadId == leadId, Token));

            var consents = await db.ConsentRecords.Where(consent => consent.LeadId == leadId).ToListAsync(Token);
            Assert.All(consents, consent => Assert.False(consent.HasMarketingConsent));

            var booking = await db.ConsultationBookings.SingleAsync(item => item.LeadId == leadId, Token);
            Assert.Equal(BookingStatus.Cancelled, booking.Status);
            Assert.Null(booking.ManageTokenHash);

            Assert.Equal(0, await db.EmailMessages.CountAsync(message => message.ToAddress.ToLower() == Subject, Token));
            Assert.True(await db.EmailMessages.AnyAsync(message => message.ToAddress == Other, Token));

            // Retained audit entries no longer carry the visitor IP, company or token hash.
            var leadAudit = await db.AuditLogs.Where(log => log.EntityId == leadId.ToString() || log.EntityId == booking.Id.ToString()).ToListAsync(Token);
            Assert.NotEmpty(leadAudit);
            Assert.All(leadAudit, log => Assert.DoesNotContain("شركة النخبة", log.Changes ?? string.Empty, StringComparison.Ordinal));
            Assert.All(leadAudit, log => Assert.DoesNotContain("ManageTokenHash", log.Changes ?? string.Empty, StringComparison.Ordinal));
            Assert.DoesNotContain(await db.AuditLogs.Select(log => log.Changes).ToListAsync(Token), changes => changes != null && changes.Contains(Subject, StringComparison.OrdinalIgnoreCase));

            // The admin account is anonymized, unusable and out of every role; the sessions remain only as revoked, IP-less records.
            var account = await db.Users.SingleAsync(user => user.Id == subjectAccount, Token);
            Assert.False(account.IsActive);
            Assert.EndsWith("@erased.invalid", account.Email!, StringComparison.Ordinal);
            Assert.Null(account.PasswordHash);
            Assert.Equal("Erased user", account.DisplayName);
            Assert.False(await db.UserRoles.AnyAsync(role => role.UserId == subjectAccount, Token));
            Assert.False(await db.UserTokens.AnyAsync(token => token.UserId == subjectAccount, Token));
            Assert.All(await db.UserSessions.Where(session => session.UserId == subjectAccount).ToListAsync(Token), session =>
            {
                Assert.NotNull(session.RevokedAtUtc);
                Assert.Null(session.IpAddress);
            });
            Assert.False(await db.RefreshTokens.AnyAsync(token => token.Session!.UserId == subjectAccount, Token));
            Assert.DoesNotContain(await db.AuditLogs.Where(log => log.UserId == subjectAccount).Select(log => log.UserName).ToListAsync(Token), name => name == Subject);

            // Every request about the subject keeps only the address hash.
            Assert.Null((await db.DataSubjectRequests.SingleAsync(item => item.Id == earlierExport.Id, Token)).SubjectEmail);

            // The other person is untouched.
            var other = await db.Inquiries.SingleAsync(item => item.NormalizedEmail == Other, Token);
            Assert.Equal("Other Co", other.Company);
            Assert.True((await db.ConsentRecords.SingleAsync(consent => consent.InquiryId == other.Id, Token)).HasMarketingConsent);
        }

        // Admin lists and search no longer reveal the subject; sign-in with the old credentials fails.
        var search = await ReadAsync<PagedResult<LeadSummaryResponse>>(await owner.GetAsync($"/api/v1/admin/leads?search={Uri.EscapeDataString(Subject)}", Token));
        Assert.Empty(search.Items);
        var detail = await ReadAsync<LeadDetailResponse>(await owner.GetAsync($"/api/v1/admin/leads/{leadId}", Token));
        Assert.Equal(ErasedValues.Name, detail.Summary.DisplayName);
        Assert.All(detail.Inquiries, item => Assert.Null(item.Message));
        Assert.Empty(detail.Notes);
        var inquiries = await ReadAsync<PagedResult<InquirySummaryResponse>>(await owner.GetAsync("/api/v1/admin/inquiries", Token));
        Assert.DoesNotContain(inquiries.Items, item => item.Name == "سارة العتيبي");
        Assert.NotEqual(HttpStatusCode.OK, (await LoginAsync(Subject, StrongPassword)).Status);

        // The previous export request can no longer run, and the erasure was audited with counts only.
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(officer.PostAsync($"/api/v1/admin/data-subject-requests/{earlierExport.Id}/export", null, Token)));
        Assert.Single(await QueryAsync(db => db.AuditLogs.Where(log => log.Action == AuditAction.DataSubjectErased).Select(log => log.Id)));
    }

    [Fact]
    public async Task Repeating_an_erasure_changes_nothing_and_concurrent_runs_erase_once()
    {
        Fixture.RequireDatabase();
        var officer = await OfficerAsync();
        await SubmitOkAsync(InquiryTypes.Contact, Form(email: Subject));
        var request = await VerifiedRequestAsync(officer, "Erasure", Subject);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            officer.PostAsJsonAsync($"/api/v1/admin/data-subject-requests/{request.Id}/erase", new ConfirmErasureRequest(Subject), Token)));
        try
        {
            Assert.All(attempts, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        }
        finally
        {
            foreach (var response in attempts)
            {
                response.Dispose();
            }
        }

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(officer.PostAsJsonAsync(
            $"/api/v1/admin/data-subject-requests/{request.Id}/erase", new ConfirmErasureRequest("anything@example.com"), Token)));
        Assert.Single(await QueryAsync(db => db.AuditLogs.Where(log => log.Action == AuditAction.DataSubjectErased).Select(log => log.Id)));
        Assert.Single(await QueryAsync(db => db.LeadActivities.Where(activity => activity.Kind == LeadActivityKind.Erased).Select(activity => activity.Id)));
    }

    [Fact]
    public async Task Queued_work_cannot_recreate_erased_data_and_new_inquiries_start_a_new_lead()
    {
        Fixture.RequireDatabase();
        var officer = await OfficerAsync();
        await SubmitOkAsync(InquiryTypes.Contact, Form(email: Subject));
        var request = await VerifiedRequestAsync(officer, "Erasure", Subject);

        // Erase before the Worker has processed the submission: the queued event must not email anything.
        await ReadAsync<DataSubjectRequestResponse>(await officer.PostAsJsonAsync($"/api/v1/admin/data-subject-requests/{request.Id}/erase", new ConfirmErasureRequest(Subject), Token));
        await using (var worker = CreateWorker())
        {
            await worker.Get<OutboxProcessor>().ProcessBatchAsync(Token);
        }

        Assert.Equal(0, await CountAsync(db => db.EmailMessages));

        await SubmitOkAsync(InquiryTypes.Contact, Form(email: Subject));
        await using var db = await DbAsync();
        Assert.Equal(2, await db.Leads.CountAsync(Token));
        Assert.Equal(1, await db.Leads.CountAsync(lead => lead.NormalizedEmail == Subject, Token));
    }

    [Fact]
    public async Task Operators_cannot_erase_their_own_account_or_the_last_superadmin()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync("owner@onyx.test");
        var officer = await OfficerAsync();

        var self = await VerifiedRequestAsync(officer, "Erasure", "officer@onyx.test");
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(officer.PostAsJsonAsync(
            $"/api/v1/admin/data-subject-requests/{self.Id}/erase", new ConfirmErasureRequest("officer@onyx.test"), Token)));

        var lastSuperAdmin = await VerifiedRequestAsync(officer, "Erasure", "owner@onyx.test");
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(officer.PostAsJsonAsync(
            $"/api/v1/admin/data-subject-requests/{lastSuperAdmin.Id}/erase", new ConfirmErasureRequest("owner@onyx.test"), Token)));

        await using var db = await DbAsync();
        Assert.True(await db.Users.AnyAsync(user => user.Email == "owner@onyx.test" && user.IsActive, Token));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(owner.GetAsync("/api/v1/auth/me", Token)));
    }
}
