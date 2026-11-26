using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Customers;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Commerce;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers.Admin;

/// <summary>Customer accounts for staff: list, detail and disable/enable. Passwords are never visible or settable here.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/customers")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminCustomersController(CustomerAdministrationService customers) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Customers.View)]
    [ProducesResponseType<PagedResult<AdminCustomerListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminCustomerListItemResponse>>> List(
        [FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await customers.ListAsync(search, status, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Customers.View)]
    [ProducesResponseType<AdminCustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, [FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.OkOrProblem(await customers.GetAsync(id, lang ?? "en", cancellationToken));

    [HttpPost("{id:guid}/status")]
    [HasPermission(Permissions.Customers.Manage)]
    [ProducesResponseType<AdminCustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SetStatus(Guid id, CustomerStatusRequest request, [FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.OkOrProblem(await customers.SetStatusAsync(id, request, lang ?? "en", cancellationToken));
}
