using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.News;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.News;

namespace Onyx.API.Controllers.Admin;

/// <summary>News categories (names, slugs and classification keywords). Viewing needs <c>News.View</c>; changes need <c>News.Edit</c>.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/news-categories")]
[HasPermission(Permissions.News.View)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminNewsCategoriesController(NewsCategoryAdministrationService categories) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NewsCategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NewsCategoryResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await categories.ListAsync(cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.News.Edit)]
    [ProducesResponseType<NewsCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create(SaveNewsCategoryRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await categories.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.News.Edit)]
    [ProducesResponseType<NewsCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update(Guid id, SaveNewsCategoryRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await categories.UpdateAsync(id, request, cancellationToken));
}
