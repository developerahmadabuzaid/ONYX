using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Content;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.API.Controllers.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/redirects")]
public sealed class AdminRedirectsController(IRedirectAdministrationService redirects) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Redirects.Manage)]
    [ProducesResponseType<PagedResult<RedirectResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RedirectResponse>>> List(
        [FromQuery] string? language, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await redirects.ListAsync(language, search, page, pageSize, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Redirects.Manage)]
    [ProducesResponseType<RedirectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create(SaveRedirectRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await redirects.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Redirects.Manage)]
    [ProducesResponseType<RedirectResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, SaveRedirectRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await redirects.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Redirects.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await redirects.DeleteAsync(id, cancellationToken));
}
