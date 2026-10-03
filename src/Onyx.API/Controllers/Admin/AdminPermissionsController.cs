using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Authorization;

namespace Onyx.API.Controllers.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/permissions")]
[HasPermission(Permissions.Roles.Manage)]
public sealed class AdminPermissionsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PermissionResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PermissionResponse>> List() =>
        Ok(Permissions.All
            .Order(StringComparer.Ordinal)
            .Select(permission => new PermissionResponse(permission, permission[..permission.LastIndexOf('.')]))
            .ToList());
}
