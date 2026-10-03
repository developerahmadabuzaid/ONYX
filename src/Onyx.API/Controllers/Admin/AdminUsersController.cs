using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Identity;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/users")]
[HasPermission(Permissions.Users.Manage)]
public sealed class AdminUsersController(IUserAdministrationService users) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<UserSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserSummaryResponse>>> List(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await users.ListAsync(search, page, pageSize, cancellationToken));

    [HttpGet("{userId:guid}")]
    [ProducesResponseType<UserSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid userId, CancellationToken cancellationToken) =>
        this.OkOrProblem(await users.GetAsync(userId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<UserSummaryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Invite(InviteUserRequest request, CancellationToken cancellationToken)
    {
        var result = await users.InviteAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { userId = result.Value.Id, version = "1" }, result.Value)
            : this.Problem(result.Error!);
    }

    [HttpPost("{userId:guid}/invitation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ResendInvitation(Guid userId, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await users.ResendInvitationAsync(userId, cancellationToken));

    [HttpPut("{userId:guid}")]
    [ProducesResponseType<UserSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await users.UpdateAsync(userId, request, cancellationToken));

    [HttpPut("{userId:guid}/roles")]
    [ProducesResponseType<UserSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SetRoles(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await users.SetRolesAsync(userId, request, cancellationToken));

    [HttpPost("{userId:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Disable(Guid userId, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await users.DisableAsync(userId, cancellationToken));

    [HttpPost("{userId:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Enable(Guid userId, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await users.EnableAsync(userId, cancellationToken));

    [HttpPost("{userId:guid}/two-factor/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ResetTwoFactor(Guid userId, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await users.ResetTwoFactorAsync(userId, cancellationToken));

    [HttpPost("{userId:guid}/sessions/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RevokeSessions(Guid userId, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await users.RevokeSessionsAsync(userId, cancellationToken));
}
