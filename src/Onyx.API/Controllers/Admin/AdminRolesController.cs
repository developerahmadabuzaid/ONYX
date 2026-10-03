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
[Route("api/v{version:apiVersion}/admin/roles")]
[HasPermission(Permissions.Roles.Manage)]
public sealed class AdminRolesController(IRoleAdministrationService roles) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RoleResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await roles.ListAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType<RoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create(SaveRoleRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await roles.CreateAsync(request, cancellationToken));

    [HttpPut("{roleId:guid}")]
    [ProducesResponseType<RoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(Guid roleId, SaveRoleRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await roles.UpdateAsync(roleId, request, cancellationToken));

    [HttpDelete("{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid roleId, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await roles.DeleteAsync(roleId, cancellationToken));
}
