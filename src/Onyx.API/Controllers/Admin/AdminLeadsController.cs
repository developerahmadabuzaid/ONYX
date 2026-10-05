using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Leads;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// The lead pipeline. Endpoint permissions are checked here; the service also limits users without <c>Leads.ViewAll</c> to
/// their own and unassigned leads.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/leads")]
public sealed class AdminLeadsController(ILeadAdministrationService leads) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Leads.View)]
    [ProducesResponseType<PagedResult<LeadSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] LeadListQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await leads.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Leads.View)]
    [ProducesResponseType<LeadDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await leads.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}/stage")]
    [HasPermission(Permissions.Leads.Manage)]
    [ProducesResponseType<LeadSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ChangeStage(Guid id, ChangeLeadStageRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await leads.ChangeStageAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/priority")]
    [HasPermission(Permissions.Leads.Manage)]
    [ProducesResponseType<LeadSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ChangePriority(Guid id, ChangeLeadPriorityRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await leads.ChangePriorityAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/owner")]
    [HasPermission(Permissions.Leads.Assign)]
    [ProducesResponseType<LeadSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Assign(Guid id, AssignLeadRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await leads.AssignAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/notes")]
    [HasPermission(Permissions.Leads.Manage)]
    [ProducesResponseType<LeadNoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AddNote(Guid id, AddLeadNoteRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await leads.AddNoteAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/consents/{consentId:guid}/withdraw-marketing")]
    [HasPermission(Permissions.Leads.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> WithdrawMarketing(Guid id, Guid consentId, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await leads.WithdrawMarketingConsentAsync(id, consentId, cancellationToken));

    [HttpGet("assignees")]
    [HasPermission(Permissions.Leads.Assign)]
    [ProducesResponseType<IReadOnlyList<LeadAssigneeResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeadAssigneeResponse>>> Assignees(CancellationToken cancellationToken) =>
        Ok(await leads.ListAssigneesAsync(cancellationToken));

    /// <summary>CSV of the leads the caller can see. Audited (row count and filters, never contact details).</summary>
    [HttpGet("export")]
    [HasPermission(Permissions.Leads.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Export([FromQuery] LeadListQuery query, CancellationToken cancellationToken)
    {
        var result = await leads.ExportAsync(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.Problem(result.Error!);
        }

        Response.Headers.CacheControl = "no-store";
        return File(Encoding.UTF8.GetBytes(result.Value.Content), "text/csv; charset=utf-8", result.Value.FileName);
    }
}
