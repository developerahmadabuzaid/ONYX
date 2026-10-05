using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Leads;
using Onyx.Contracts.Privacy;
using Onyx.Domain.Auditing;
using Onyx.Infrastructure.BackgroundJobs;

namespace Onyx.API.IntegrationTests.Privacy;

/// <summary>Data-subject export: complete across Stage 0–8 data, only the subject's, no credentials, Arabic intact, audited.</summary>
public sealed class DataSubjectExportApiTests(PostgresFixture fixture) : PrivacyApiTestBase(fixture)
{
    [Fact]
    public async Task The_export_contains_every_category_of_the_subjects_data_and_nothing_of_anyone_else()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var officer = await OfficerAsync();

        // The subject: two inquiries (different letter case), a booking, a staff note, marketing consent, an admin account.
        var attribution = new AttributionDto(
            new TouchPointDto("/ar/الخدمات", null, "google", "cpc", "launch", null, null, Time.GetUtcNow()), null, "/ar/تواصل-معنا", "SA", "mobile", "111.222", true);
        await SubmitOkAsync(InquiryTypes.Contact, Arabic(Subject, Solved(), company: "شركة النخبة", marketing: true, attribution: attribution));
        await SubmitOkAsync(InquiryTypes.StartProject, Form(email: "Subject@Example.com", company: "Sample Co"));
        await BookFirstSlotAsync(owner, Form(email: Subject, token: Solved("booking")));
        var subjectAccount = await CreateUserAsync(Subject, StrongPassword, SystemRoles.Sales);

        // Someone else at the same company.
        await SubmitOkAsync(InquiryTypes.Contact, Form(email: Other, company: "شركة النخبة", message: "Unrelated message from another person."));

        Guid leadId;
        await using (var db = await DbAsync())
        {
            leadId = await db.Leads.Where(lead => lead.NormalizedEmail == Subject).Select(lead => lead.Id).SingleAsync(Token);
        }

        await ReadAsync<LeadNoteResponse>(await owner.PostAsJsonAsync($"/api/v1/admin/leads/{leadId}/notes", new AddLeadNoteRequest("اتصلت بها؛ تريد عرضاً تفصيلياً."), Token));
        await using (var worker = CreateWorker())
        {
            await worker.Get<OutboxProcessor>().ProcessBatchAsync(Token);
        }

        var request = await VerifiedRequestAsync(officer, "Export", "  SUBJECT@example.com ");
        using var response = await officer.PostAsync($"/api/v1/admin/data-subject-requests/{request.Id}/export", null, Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        var file = (await response.Content.ReadFromJsonAsync<DataSubjectExportFile>(Token))!;
        Assert.EndsWith(".json", file.FileName, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(file.Content);
        var root = document.RootElement;
        Assert.Equal("onyx.data-subject-export", root.GetProperty("format").GetString());
        Assert.Equal(Subject, root.GetProperty("subject").GetProperty("email").GetString());

        var lead = Assert.Single(root.GetProperty("leads").EnumerateArray());
        Assert.Equal("اتصلت بها؛ تريد عرضاً تفصيلياً.", Assert.Single(lead.GetProperty("notes").EnumerateArray()).GetProperty("body").GetString());
        Assert.NotEmpty(lead.GetProperty("activities").EnumerateArray());

        var inquiries = root.GetProperty("inquiries").EnumerateArray().ToList();
        Assert.Equal(3, inquiries.Count); // contact, project brief and the booked consultation
        var arabic = inquiries.Single(item => item.GetProperty("languageCode").GetString() == "ar");
        Assert.Equal("سارة العتيبي", arabic.GetProperty("name").GetString());
        Assert.Equal("نحتاج إلى أتمتة الموافقات الداخلية وربطها بالنظام المالي.", arabic.GetProperty("message").GetString());
        Assert.Equal("شركة النخبة", arabic.GetProperty("company").GetString());
        Assert.Equal("111.222", arabic.GetProperty("attribution").GetProperty("analyticsClientId").GetString());
        Assert.True(arabic.GetProperty("consent").GetProperty("marketingConsent").GetBoolean());
        Assert.Contains("سارة العتيبي", file.Content, StringComparison.Ordinal);

        Assert.Single(root.GetProperty("bookings").EnumerateArray());
        var emails = root.GetProperty("emails").EnumerateArray().ToList();
        Assert.Contains(emails, email => email.GetProperty("template").GetString() == "bookings.confirmed");
        Assert.Contains(emails, email => email.GetProperty("template").GetString() == "leads.inquiry-received");

        var account = root.GetProperty("account");
        Assert.Equal(subjectAccount, account.GetProperty("id").GetGuid());
        Assert.Contains("Sales", account.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));

        // Nothing about the other person, and no credential or token of any kind.
        Assert.DoesNotContain(Other, file.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unrelated message", file.Content, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "passwordHash", "securityStamp", "manageTokenHash", "tokenHash", "ipAddress", "recoveryCode", "captcha", Recipient })
        {
            Assert.DoesNotContain(forbidden, file.Content, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("token=[removed]", file.Content, StringComparison.Ordinal);
        Assert.DoesNotMatch("token=[A-Za-z0-9_-]{20,}", file.Content);

        // The request completed, the export was audited with counts only, and the same data gives the same file.
        var completed = await ReadAsync<DataSubjectRequestResponse>(await officer.GetAsync($"/api/v1/admin/data-subject-requests/{request.Id}", Token));
        Assert.Equal("Completed", completed.Status);
        var audits = await QueryAsync(db => db.AuditLogs.Where(log => log.Action == AuditAction.DataSubjectDataExported).Select(log => log.Changes));
        Assert.DoesNotContain(Subject, Assert.Single(audits), StringComparison.OrdinalIgnoreCase);

        var again = (await ReadAsync<DataSubjectExportFile>(await officer.PostAsync($"/api/v1/admin/data-subject-requests/{request.Id}/export", null, Token))).Content;
        Assert.Equal(StripGeneratedAt(file.Content), StripGeneratedAt(again));
    }

    [Fact]
    public async Task Exports_need_a_verified_export_request_and_an_existing_id()
    {
        Fixture.RequireDatabase();
        var officer = await OfficerAsync();
        var received = await ReadAsync<DataSubjectRequestResponse>(await officer.PostAsJsonAsync(
            "/api/v1/admin/data-subject-requests", new CreateDataSubjectRequestRequest("Export", Subject, null), Token));
        var erasure = await VerifiedRequestAsync(officer, "Erasure", Subject);

        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(officer.PostAsync($"/api/v1/admin/data-subject-requests/{received.Id}/export", null, Token)));
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(officer.PostAsync($"/api/v1/admin/data-subject-requests/{erasure.Id}/export", null, Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(officer.PostAsync($"/api/v1/admin/data-subject-requests/{Guid.NewGuid()}/export", null, Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(officer.PostAsJsonAsync(
            $"/api/v1/admin/data-subject-requests/{received.Id}/verify", new VerifyDataSubjectRequestRequest(" ", null), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(officer.PostAsJsonAsync(
            "/api/v1/admin/data-subject-requests", new CreateDataSubjectRequestRequest("Export", "not-an-email", null), Token)));

        // Free-text request notes never reach the audit log.
        var notes = await QueryAsync(db => db.AuditLogs.Where(log => log.EntityType == "DataSubjectRequest").Select(log => log.Changes));
        Assert.DoesNotContain(notes, changes => changes != null && (changes.Contains(Subject, StringComparison.OrdinalIgnoreCase) || changes.Contains("identity confirmed", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Privacy_endpoints_require_privacy_manage()
    {
        Fixture.RequireDatabase();
        var sales = await SignInWithAsync("sales", Permissions.Leads.View, Permissions.Leads.ViewAll, Permissions.Leads.Manage);
        var id = Guid.NewGuid();

        foreach (var path in new[] { "/api/v1/admin/data-subject-requests", $"/api/v1/admin/data-subject-requests/{id}", "/api/v1/admin/retention-policies" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(sales.GetAsync(path, Token)));
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync(path, Token)));
        }

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(sales.PostAsJsonAsync("/api/v1/admin/data-subject-requests", new CreateDataSubjectRequestRequest("Export", Subject, null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(sales.PostAsync($"/api/v1/admin/data-subject-requests/{id}/export", null, Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(sales.PostAsJsonAsync($"/api/v1/admin/data-subject-requests/{id}/erase", new ConfirmErasureRequest(Subject), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(sales.PostAsJsonAsync($"/api/v1/admin/data-subject-requests/{id}/verify", new VerifyDataSubjectRequestRequest("x", null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(sales.PutAsJsonAsync("/api/v1/admin/retention-policies/AuditLogs", new SaveRetentionPolicyRequest("Delete", 30, true, null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(sales.GetAsync("/api/v1/admin/retention-policies/AuditLogs/preview?action=Delete&periodDays=30", Token)));
    }

    private static string StripGeneratedAt(string json) =>
        System.Text.RegularExpressions.Regex.Replace(json, "\"generatedAtUtc\": \"[^\"]+\"", string.Empty);
}
