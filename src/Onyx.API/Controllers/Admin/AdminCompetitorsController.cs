using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Intelligence;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Intelligence;

namespace Onyx.API.Controllers.Admin;

/// <summary>Internal-only competitor references. Never exposed publicly.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/competitors")]
public sealed class AdminCompetitorsController(ICompetitorService competitors) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Competitors.View)]
    [ProducesResponseType<IReadOnlyList<CompetitorResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompetitorResponse>>> List(CancellationToken cancellationToken) => Ok(await competitors.ListAsync(cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Competitors.Manage)]
    [ProducesResponseType<CompetitorResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Create(SaveCompetitorRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await competitors.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Competitors.Manage)]
    [ProducesResponseType<CompetitorResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, SaveCompetitorRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await competitors.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Competitors.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await competitors.DeleteAsync(id, cancellationToken));
}
