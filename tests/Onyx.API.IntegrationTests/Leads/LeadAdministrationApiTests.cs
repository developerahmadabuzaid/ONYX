using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;
using Onyx.Domain.Auditing;

namespace Onyx.API.IntegrationTests.Leads;

/// <summary>The admin pipeline: permission matrix, per-lead visibility, assignment rules, audit, concurrency and export.</summary>
public sealed class LeadAdministrationApiTests(PostgresFixture fixture) : LeadsApiTestBase(fixture)
{
    [Fact]
    public async Task Users_without_view_all_see_only_their_own_and_unassigned_leads()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var first = await SignInWithAsync("first", Permissions.Leads.View, Permissions.Inquiries.View);
        var second = await SignInWithAsync("second", Permissions.Leads.View);
        var firstId = await UserIdAsync("first@onyx.test");
        var secondId = await UserIdAsync("second@onyx.test");

        var mine = await LeadFromAsync("mine@example.com");
        var theirs = await LeadFromAsync("theirs@example.com");
        var open = await LeadFromAsync("open@example.com");
        await ReadAsync<LeadSummaryResponse>(await owner.PutAsJsonAsync($"/api/v1/admin/leads/{mine}/owner", new AssignLeadRequest(firstId, null), Token));
        await ReadAsync<LeadSummaryResponse>(await owner.PutAsJsonAsync($"/api/v1/admin/leads/{theirs}/owner", new AssignLeadRequest(secondId, null), Token));

        var visible = await ReadAsync<PagedResult<LeadSummaryResponse>>(await first.GetAsync("/api/v1/admin/leads", Token));
        Assert.Equal(new[] { mine, open }.Order(), visible.Items.Select(lead => lead.Id).Order());
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(first.GetAsync($"/api/v1/admin/leads/{theirs}", Token)));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(first.GetAsync($"/api/v1/admin/leads/{mine}", Token)));

        var inquiries = await ReadAsync<PagedResult<InquirySummaryResponse>>(await first.GetAsync("/api/v1/admin/inquiries", Token));
        Assert.DoesNotContain(inquiries.Items, inquiry => inquiry.LeadId == theirs);

        var mineOnly = await ReadAsync<PagedResult<LeadSummaryResponse>>(await first.GetAsync("/api/v1/admin/leads?owner=me", Token));
        Assert.Equal(mine, Assert.Single(mineOnly.Items).Id);

        var all = await ReadAsync<PagedResult<LeadSummaryResponse>>(await owner.GetAsync("/api/v1/admin/leads", Token));
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(second.GetAsync($"/api/v1/admin/leads/{theirs}", Token)));
    }

    [Fact]
    public async Task Assignment_needs_leads_assign_and_an_assignee_who_can_view_leads()
    {
        Fixture.RequireDatabase();
        var manager = await SignInWithAsync("manager", Permissions.Leads.View, Permissions.Leads.ViewAll, Permissions.Leads.Manage);
        var assigner = await SignInWithAsync("assigner", Permissions.Leads.View, Permissions.Leads.ViewAll, Permissions.Leads.Assign);
        await SignInWithAsync("editor", Permissions.Content.View);
        var editorId = await UserIdAsync("editor@onyx.test");
        var managerId = await UserIdAsync("manager@onyx.test");
        var lead = await LeadFromAsync("assign@example.com");

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(manager.PutAsJsonAsync($"/api/v1/admin/leads/{lead}/owner", new AssignLeadRequest(managerId, null), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(assigner.PutAsJsonAsync($"/api/v1/admin/leads/{lead}/owner", new AssignLeadRequest(editorId, null), Token)));

        var assigned = await ReadAsync<LeadSummaryResponse>(await assigner.PutAsJsonAsync($"/api/v1/admin/leads/{lead}/owner", new AssignLeadRequest(managerId, null), Token));
        Assert.Equal(managerId, assigned.OwnerId);
        Assert.Equal("manager", assigned.OwnerName);

        var assignees = await ReadAsync<List<LeadAssigneeResponse>>(await assigner.GetAsync("/api/v1/admin/leads/assignees", Token));
        Assert.Contains(assignees, user => user.UserId == managerId);
        Assert.DoesNotContain(assignees, user => user.UserId == editorId);
        Assert.Contains(AuditAction.LeadAssigned, await QueryAsync(db => db.AuditLogs.Where(log => log.EntityId == lead.ToString()).Select(log => log.Action)));
    }

    [Fact]
    public async Task Stage_priority_notes_and_consent_withdrawal_are_audited_and_version_checked()
    {
        Fixture.RequireDatabase();
        var manager = await SignInWithAsync("manager", Permissions.Leads.View, Permissions.Leads.ViewAll, Permissions.Leads.Manage);
        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form(email: "stage@example.com", marketingConsent: true))));
        var lead = (await ReadAsync<PagedResult<LeadSummaryResponse>>(await manager.GetAsync("/api/v1/admin/leads", Token))).Items.Single();

        var qualified = await ReadAsync<LeadSummaryResponse>(await manager.PutAsJsonAsync(
            $"/api/v1/admin/leads/{lead.Id}/stage", new ChangeLeadStageRequest("Qualified", lead.Version), Token));
        Assert.Equal("Qualified", qualified.Stage);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(manager.PutAsJsonAsync(
            $"/api/v1/admin/leads/{lead.Id}/stage", new ChangeLeadStageRequest("Won", lead.Version), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(manager.PutAsJsonAsync(
            $"/api/v1/admin/leads/{lead.Id}/stage", new ChangeLeadStageRequest("Imaginary", null), Token)));
        await ReadAsync<LeadSummaryResponse>(await manager.PutAsJsonAsync($"/api/v1/admin/leads/{lead.Id}/priority", new ChangeLeadPriorityRequest("High", null), Token));
        var note = await ReadAsync<LeadNoteResponse>(await manager.PostAsJsonAsync($"/api/v1/admin/leads/{lead.Id}/notes", new AddLeadNoteRequest("Called; follow up next week."), Token));
        Assert.Equal("manager", note.AuthorName);

        var detail = await ReadAsync<LeadDetailResponse>(await manager.GetAsync($"/api/v1/admin/leads/{lead.Id}", Token));
        var consent = detail.Inquiries.Single().Consent!;
        Assert.True(consent.MarketingActive);
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(manager.PostAsync($"/api/v1/admin/leads/{lead.Id}/consents/{consent.Id}/withdraw-marketing", null, Token)));
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(manager.PostAsync($"/api/v1/admin/leads/{lead.Id}/consents/{consent.Id}/withdraw-marketing", null, Token)));

        detail = await ReadAsync<LeadDetailResponse>(await manager.GetAsync($"/api/v1/admin/leads/{lead.Id}", Token));
        Assert.False(detail.Inquiries.Single().Consent!.MarketingActive);
        Assert.Single(detail.Notes);
        Assert.Equal(
            ["InquiryReceived", "MarketingConsentWithdrawn", "NoteAdded", "PriorityChanged", "StageChanged"],
            detail.Activities.Select(activity => activity.Kind).Order().ToArray());
        Assert.Equal("stage@example.com", detail.Inquiries.Single().Email);

        var actions = await QueryAsync(db => db.AuditLogs.Select(log => log.Action));
        Assert.Contains(AuditAction.LeadStageChanged, actions);
        Assert.Contains(AuditAction.LeadPriorityChanged, actions);
        Assert.Contains(AuditAction.LeadNoteAdded, actions);
        Assert.Contains(AuditAction.MarketingConsentWithdrawn, actions);

        // Note bodies and contact details never reach the audit log.
        var audited = await QueryAsync(db => db.AuditLogs.Select(log => log.Changes));
        Assert.DoesNotContain(audited, changes => changes != null && changes.Contains("follow up next week", StringComparison.Ordinal));
        Assert.DoesNotContain(audited, changes => changes != null && changes.Contains("stage@example.com", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Export_requires_leads_export_neutralizes_formulas_and_is_audited_without_contact_details()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var viewer = await SignInWithAsync("viewer", Permissions.Leads.View, Permissions.Leads.ViewAll);
        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form(email: "csv@example.com", company: "=HYPERLINK(\"x\")"))));

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.GetAsync("/api/v1/admin/leads/export", Token)));

        using var response = await owner.GetAsync("/api/v1/admin/leads/export", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        var csv = await response.Content.ReadAsStringAsync(Token);
        Assert.Contains("\"csv@example.com\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"'=HYPERLINK(\"\"x\"\")\"", csv, StringComparison.Ordinal);

        var exports = await QueryAsync(db => db.AuditLogs.Where(log => log.Action == AuditAction.LeadsExported).Select(log => log.Changes));
        var audit = Assert.Single(exports) ?? string.Empty;
        Assert.DoesNotContain("csv@example.com", audit, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_lead_endpoints_return_403_without_their_permission()
    {
        Fixture.RequireDatabase();
        var outsider = await SignInWithAsync("outsider", Permissions.Content.View);
        var lead = Guid.NewGuid();

        foreach (var path in new[]
        {
            "/api/v1/admin/leads", $"/api/v1/admin/leads/{lead}", "/api/v1/admin/leads/assignees", "/api/v1/admin/leads/export", "/api/v1/admin/inquiries",
            "/api/v1/admin/bookings", $"/api/v1/admin/bookings/{lead}", "/api/v1/admin/availability",
        })
        {
            Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.GetAsync(path, Token)));
        }

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.PutAsJsonAsync($"/api/v1/admin/leads/{lead}/stage", new ChangeLeadStageRequest("Won", null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.PutAsJsonAsync($"/api/v1/admin/leads/{lead}/priority", new ChangeLeadPriorityRequest("High", null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.PutAsJsonAsync($"/api/v1/admin/leads/{lead}/owner", new AssignLeadRequest(null, null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.PostAsJsonAsync($"/api/v1/admin/leads/{lead}/notes", new AddLeadNoteRequest("x"), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.PostAsync($"/api/v1/admin/leads/{lead}/consents/{lead}/withdraw-marketing", null, Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.PostAsJsonAsync($"/api/v1/admin/bookings/{lead}/cancel", new Contracts.Booking.CancelBookingRequest(false, null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.PostAsJsonAsync(
            $"/api/v1/admin/bookings/{lead}/outcome", new Contracts.Booking.RecordBookingOutcomeRequest("Completed", null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.PutAsJsonAsync(
            "/api/v1/admin/availability/settings", new Contracts.Booking.SaveBookingSettingsRequest(true, "Asia/Riyadh", 30, 0, 0, 7, null), Token)));

        // Bookings.View alone cannot change bookings or availability.
        var bookingViewer = await SignInWithAsync("bookingviewer", Permissions.Bookings.View);
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(bookingViewer.GetAsync("/api/v1/admin/bookings", Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(bookingViewer.PostAsJsonAsync($"/api/v1/admin/bookings/{lead}/cancel", new Contracts.Booking.CancelBookingRequest(false, null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(bookingViewer.GetAsync("/api/v1/admin/availability", Token)));
    }

    private async Task<Guid> LeadFromAsync(string email)
    {
        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form(email: email))));
        await using var db = await DbAsync();
        return await db.Leads.Where(lead => lead.NormalizedEmail == email).Select(lead => lead.Id).SingleAsync(Token);
    }

    private async Task<Guid> UserIdAsync(string email)
    {
        await using var db = await DbAsync();
        return await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync(Token);
    }
}
