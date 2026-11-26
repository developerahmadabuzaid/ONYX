using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Billing;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Commerce;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers.Admin;

/// <summary>Manual invoices: draft, edit, issue, mark paid, void. No payment provider is connected.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/invoices")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminInvoicesController(InvoiceAdministrationService invoices) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Invoices.View)]
    [ProducesResponseType<PagedResult<AdminInvoiceListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminInvoiceListItemResponse>>> List(
        [FromQuery] string? status, [FromQuery] Guid? customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await invoices.ListAsync(status, customerId, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Invoices.View)]
    [ProducesResponseType<AdminInvoiceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await invoices.GetAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Invoices.Manage)]
    [ProducesResponseType<AdminInvoiceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create(SaveInvoiceRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await invoices.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Invoices.Manage)]
    [ProducesResponseType<AdminInvoiceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update(Guid id, SaveInvoiceRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await invoices.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/{command:regex(^(issue|paid|void)$)}")]
    [HasPermission(Permissions.Invoices.Manage)]
    [ProducesResponseType<AdminInvoiceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Command(Guid id, string command, InvoiceCommandRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await invoices.CommandAsync(id, command, request, cancellationToken));
}
