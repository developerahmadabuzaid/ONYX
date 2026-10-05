using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Privacy;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Privacy;

namespace Onyx.API.Controllers.Admin;

/// <summary>Retention policies per data category. Nothing is preset; the preview counts what a setting would process.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/retention-policies")]
[HasPermission(Permissions.Privacy.Manage)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminRetentionPoliciesController(IRetentionService retention) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RetentionPolicyResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RetentionPolicyResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await retention.ListAsync(cancellationToken));

    [HttpPut("{category}")]
    [ProducesResponseType<RetentionPolicyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Save(string category, SaveRetentionPolicyRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await retention.SaveAsync(category, request, cancellationToken));

    [HttpGet("{category}/preview")]
    [ProducesResponseType<RetentionPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Preview(string category, [FromQuery] string action, [FromQuery] int periodDays, CancellationToken cancellationToken) =>
        this.OkOrProblem(await retention.PreviewAsync(category, action, periodDays, cancellationToken));
}
