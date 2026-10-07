using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.News;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.News;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// The news source allowlist (<c>NewsSources.Manage</c>). Sources start blocked and are fetched only after being allowed with
/// a reviewed licensing note. No endpoint fetches anything: fetching happens only in the Worker through the SSRF-safe client.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/news-sources")]
[HasPermission(Permissions.NewsSources.Manage)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminNewsSourcesController(NewsSourceAdministrationService sources) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NewsSourceResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NewsSourceResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await sources.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<NewsSourceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await sources.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<NewsSourceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create(SaveNewsSourceRequest request, CancellationToken cancellationToken)
    {
        var result = await sources.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id, version = "1" }, result.Value)
            : this.Problem(result.Error!);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<NewsSourceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update(Guid id, SaveNewsSourceRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await sources.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/allow")]
    [ProducesResponseType<NewsSourceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Allow(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await sources.AllowAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/block")]
    [ProducesResponseType<NewsSourceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Block(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await sources.BlockAsync(id, request, cancellationToken));
}
