using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.Application.Auditing;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers.Admin;

/// <summary>Read-only by design: audit records have no create, update or delete endpoint.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/audit-logs")]
[HasPermission(Permissions.AuditLogs.View)]
public sealed class AdminAuditLogsController(IAuditLogQueries auditLogs) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<AuditLogResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> Search([FromQuery] AuditLogQuery query, CancellationToken cancellationToken) =>
        Ok(await auditLogs.SearchAsync(query, cancellationToken));
}
