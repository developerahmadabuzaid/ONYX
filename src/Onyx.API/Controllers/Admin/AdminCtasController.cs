using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Content;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Content;

namespace Onyx.API.Controllers.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/ctas")]
public sealed class AdminCtasController(ICallToActionService ctas) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Ctas.View)]
    [ProducesResponseType<IReadOnlyList<CtaResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CtaResponse>>> List(CancellationToken cancellationToken) => Ok(await ctas.ListAsync(cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Ctas.Create)]
    [ProducesResponseType<CtaResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Create(SaveCtaRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await ctas.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Ctas.Edit)]
    [ProducesResponseType<CtaResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, SaveCtaRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await ctas.UpdateAsync(id, request, cancellationToken));

    [HttpGet("defaults")]
    [HasPermission(Permissions.Ctas.View)]
    [ProducesResponseType<CtaDefaultsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CtaDefaultsResponse>> Defaults(CancellationToken cancellationToken) => Ok(await ctas.GetDefaultsAsync(cancellationToken));

    [HttpPut("defaults")]
    [HasPermission(Permissions.Ctas.Edit)]
    [ProducesResponseType<CtaDefaultsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetDefault(SetCtaDefaultRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await ctas.SetDefaultAsync(request, cancellationToken));
}
