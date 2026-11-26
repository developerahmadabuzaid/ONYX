using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Delivery;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.API.Controllers.Admin;

/// <summary>Sprint lifecycle and the workspace dashboard. Sprint changes need the Manager role (or <c>Delivery.Administer</c>).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/delivery")]
[HasPermission(Permissions.Delivery.Access)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDeliverySprintsController(SprintService sprints, DeliveryQueryService queries) : ControllerBase
{
    [HttpGet("dashboard")]
    [ProducesResponseType<DeliveryDashboardResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeliveryDashboardResponse>> Dashboard(CancellationToken cancellationToken) =>
        Ok(await queries.DashboardAsync(cancellationToken));

    [HttpPut("sprints/{id:guid}")]
    [ProducesResponseType<SprintResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Plan(Guid id, SaveSprintRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await sprints.PlanAsync(id, request, cancellationToken));

    [HttpPost("sprints/{id:guid}/start")]
    [ProducesResponseType<SprintResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Start(Guid id, CloseSprintRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return this.OkOrProblem(await sprints.StartAsync(id, request.Version, cancellationToken));
    }

    [HttpPost("sprints/{id:guid}/complete")]
    [ProducesResponseType<SprintResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Complete(Guid id, CloseSprintRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await sprints.CompleteAsync(id, request, cancellationToken));

    [HttpPost("sprints/{id:guid}/cancel")]
    [ProducesResponseType<SprintResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Cancel(Guid id, CloseSprintRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await sprints.CancelAsync(id, request, cancellationToken));
}
