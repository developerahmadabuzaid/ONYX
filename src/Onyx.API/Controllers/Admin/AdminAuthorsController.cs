using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Insights;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Insights;

namespace Onyx.API.Controllers.Admin;

/// <summary>Real, consenting people who may be credited on insights.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/authors")]
public sealed class AdminAuthorsController(IAuthorService authors) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Insights.View)]
    [ProducesResponseType<IReadOnlyList<AuthorResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuthorResponse>>> List(CancellationToken cancellationToken) => Ok(await authors.ListAsync(cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Insights.Edit)]
    [ProducesResponseType<AuthorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create(SaveAuthorRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await authors.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Insights.Edit)]
    [ProducesResponseType<AuthorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update(Guid id, SaveAuthorRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await authors.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Insights.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await authors.DeleteAsync(id, cancellationToken));
}
