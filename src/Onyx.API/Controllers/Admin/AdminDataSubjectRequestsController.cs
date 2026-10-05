using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Privacy;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Privacy;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// Data-subject requests (export and erasure), operated by staff holding <c>Privacy.Manage</c> (a 2FA-privileged permission).
/// Every step takes only the request id: the subject is the address stored when the request was received. Responses are never
/// cached; the export is generated per request and never stored.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/data-subject-requests")]
[HasPermission(Permissions.Privacy.Manage)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDataSubjectRequestsController(IDataSubjectRequestService requests) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<DataSubjectRequestResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] DataSubjectRequestListQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await requests.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DataSubjectRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await requests.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<DataSubjectRequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create(CreateDataSubjectRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await requests.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id, version = "1" }, result.Value)
            : this.Problem(result.Error!);
    }

    [HttpPost("{id:guid}/verify")]
    [ProducesResponseType<DataSubjectRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Verify(Guid id, VerifyDataSubjectRequestRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await requests.VerifyAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<DataSubjectRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Reject(Guid id, RejectDataSubjectRequestRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await requests.RejectAsync(id, request, cancellationToken));

    /// <summary>Generates the machine-readable export. Audited (counts only).</summary>
    [HttpPost("{id:guid}/export")]
    [ProducesResponseType<DataSubjectExportFile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Export(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await requests.ExportAsync(id, cancellationToken));

    /// <summary>Erases the subject. Requires the subject's address typed again; repeating a completed erasure changes nothing.</summary>
    [HttpPost("{id:guid}/erase")]
    [ProducesResponseType<DataSubjectRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Erase(Guid id, ConfirmErasureRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await requests.EraseAsync(id, request, cancellationToken));
}
