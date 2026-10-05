using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Leads;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Models;
using Onyx.Web.Authentication;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// The lead pipeline: list, detail with the origin trail, stage, priority, owner, notes, marketing-consent withdrawal and CSV
/// export. Visibility (own and unassigned leads without <c>Leads.ViewAll</c>) and every permission are enforced by the API.
/// </summary>
[Route("admin/leads")]
public sealed class LeadsController(IAdminLeadsApiClient api, CurrentAdminContext currentAdmin) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? stage, string? owner, string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = new LeadListQuery(Blank(stage), Blank(owner), Blank(search), Math.Max(page, 1), 25);
        var result = await api.ListLeadsAsync(query, cancellationToken);
        if (Failed(result) is { } failed)
        {
            return failed;
        }

        var permissions = await PermissionsAsync(cancellationToken);
        return View(new LeadListViewModel(result.Value!, query, permissions.Contains(Permissions.Leads.Export)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await api.GetLeadAsync(id, cancellationToken);
        if (Failed(result) is { } failed)
        {
            return failed;
        }

        var permissions = await PermissionsAsync(cancellationToken);
        var canAssign = permissions.Contains(Permissions.Leads.Assign);
        IReadOnlyList<LeadAssigneeResponse> assignees = [];
        if (canAssign)
        {
            var listed = await api.ListAssigneesAsync(cancellationToken);
            assignees = listed.IsSuccess ? listed.Value! : [];
        }

        return View(new LeadDetailViewModel(result.Value!, assignees, permissions.Contains(Permissions.Leads.Manage), canAssign));
    }

    [HttpPost("{id:guid}/stage")]
    public async Task<IActionResult> Stage(Guid id, string stage, uint version, CancellationToken cancellationToken) =>
        After(await api.ChangeStageAsync(id, new ChangeLeadStageRequest(stage ?? string.Empty, version), cancellationToken), RedirectToAction(nameof(Details), new { id }));

    [HttpPost("{id:guid}/priority")]
    public async Task<IActionResult> Priority(Guid id, string priority, uint version, CancellationToken cancellationToken) =>
        After(await api.ChangePriorityAsync(id, new ChangeLeadPriorityRequest(priority ?? string.Empty, version), cancellationToken), RedirectToAction(nameof(Details), new { id }));

    [HttpPost("{id:guid}/owner")]
    public async Task<IActionResult> Owner(Guid id, Guid? ownerId, uint version, CancellationToken cancellationToken) =>
        After(await api.AssignAsync(id, new AssignLeadRequest(ownerId, version), cancellationToken), RedirectToAction(nameof(Details), new { id }));

    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> Note(Guid id, string body, CancellationToken cancellationToken) =>
        After(await api.AddNoteAsync(id, new AddLeadNoteRequest(body ?? string.Empty), cancellationToken), RedirectToAction(nameof(Details), new { id }));

    [HttpPost("{id:guid}/consents/{consentId:guid}/withdraw-marketing")]
    public async Task<IActionResult> WithdrawMarketing(Guid id, Guid consentId, CancellationToken cancellationToken) =>
        After(await api.WithdrawMarketingConsentAsync(id, consentId, cancellationToken), RedirectToAction(nameof(Details), new { id }));

    /// <summary>Streams the API export (the API audits it). A refusal returns to the list with no file.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(string? stage, string? owner, string? search, CancellationToken cancellationToken)
    {
        var file = await api.ExportLeadsAsync(new LeadListQuery(Blank(stage), Blank(owner), Blank(search)), cancellationToken);
        if (file is null)
        {
            return ShowError("Leads.ExportFailed", RedirectToAction(nameof(Index)));
        }

        Response.RegisterForDisposeAsync(file);
        return File(file.Content, "text/csv; charset=utf-8", "onyx-leads.csv");
    }

    private async Task<IReadOnlySet<string>> PermissionsAsync(CancellationToken cancellationToken) =>
        (await currentAdmin.GetAsync(cancellationToken)).Permissions.ToHashSet(StringComparer.Ordinal);
}
