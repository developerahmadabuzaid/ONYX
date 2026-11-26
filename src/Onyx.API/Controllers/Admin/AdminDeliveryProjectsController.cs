using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Delivery;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// Delivery projects with their board, backlog and sprints. Requires <c>Delivery.Access</c>; what the caller may do inside a project
/// comes from their project role (or <c>Delivery.Administer</c>) and is enforced by the services. Unknown or invisible projects are 404.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/delivery/projects")]
[HasPermission(Permissions.Delivery.Access)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDeliveryProjectsController(
    DeliveryProjectService projects,
    DeliveryQueryService queries,
    SprintService sprints) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DeliveryProjectListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeliveryProjectListItemResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await projects.ListAsync(cancellationToken));

    [HttpGet("users")]
    [ProducesResponseType<IReadOnlyList<DeliveryUserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeliveryUserResponse>>> EligibleUsers(CancellationToken cancellationToken) =>
        Ok(await projects.EligibleUsersAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DeliveryProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await projects.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<DeliveryProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create(SaveDeliveryProjectRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await projects.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<DeliveryProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update(Guid id, SaveDeliveryProjectRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await projects.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType<DeliveryProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SaveMember(Guid id, SaveProjectMemberRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await projects.SaveMemberAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType<DeliveryProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid userId, [FromQuery] uint version, CancellationToken cancellationToken) =>
        this.OkOrProblem(await projects.RemoveMemberAsync(id, userId, version, cancellationToken));

    [HttpPut("{id:guid}/statuses/{statusId:guid}/wip")]
    [ProducesResponseType<DeliveryProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetWipLimit(Guid id, Guid statusId, SetWipLimitRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await projects.SetWipLimitAsync(id, statusId, request, cancellationToken));

    [HttpGet("{id:guid}/board")]
    [ProducesResponseType<BoardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Board(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await queries.BoardAsync(id, cancellationToken));

    [HttpGet("{id:guid}/backlog")]
    [ProducesResponseType<BacklogResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Backlog(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await queries.BacklogAsync(id, cancellationToken));

    [HttpGet("{id:guid}/sprints")]
    [ProducesResponseType<IReadOnlyList<SprintResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Sprints(Guid id, [FromQuery] bool includeClosed, CancellationToken cancellationToken) =>
        this.OkOrProblem(await sprints.ListAsync(id, includeClosed, cancellationToken));

    [HttpGet("{id:guid}/timeline")]
    [ProducesResponseType<TimelineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Timeline(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await queries.TimelineAsync(id, cancellationToken));

    [HttpGet("{id:guid}/velocity")]
    [ProducesResponseType<IReadOnlyList<SprintVelocityResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Velocity(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await queries.VelocityAsync(id, cancellationToken));

    [HttpPost("{id:guid}/sprints")]
    [ProducesResponseType<SprintResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CreateSprint(Guid id, SaveSprintRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await sprints.CreateAsync(id, request, cancellationToken));
}
