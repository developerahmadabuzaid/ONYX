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
/// News items for editors: review, Onyx summaries, publish/hide/unpublish/delete and indexing. Viewing needs <c>News.View</c>;
/// each change also needs its own permission. Every change is audited and guarded by the item's version.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/news")]
[HasPermission(Permissions.News.View)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminNewsController(NewsAdministrationService news) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<NewsItemListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] NewsItemListQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await news.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<NewsItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await news.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}/translations/{language}")]
    [HasPermission(Permissions.News.Edit)]
    [ProducesResponseType<NewsItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> EditTranslation(Guid id, string language, UpdateNewsTranslationRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await news.EditTranslationAsync(id, language, request, cancellationToken));

    [HttpPut("{id:guid}/category")]
    [HasPermission(Permissions.News.Edit)]
    [ProducesResponseType<NewsItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ChangeCategory(Guid id, ChangeNewsCategoryRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await news.ChangeCategoryAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/publish")]
    [HasPermission(Permissions.News.Publish)]
    [ProducesResponseType<NewsItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Publish(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await news.PublishAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/hide")]
    [HasPermission(Permissions.News.Publish)]
    [ProducesResponseType<NewsItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Hide(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await news.HideAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/unpublish")]
    [HasPermission(Permissions.News.Publish)]
    [ProducesResponseType<NewsItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Unpublish(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await news.UnpublishAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/indexing")]
    [HasPermission(Permissions.News.Publish)]
    [ProducesResponseType<NewsItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SetIndexing(Guid id, SetNewsIndexingRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await news.SetIndexingAsync(id, request, cancellationToken));

    /// <summary>Deletes the item from the hub; its source URL is kept so the article is never ingested again.</summary>
    [HttpPost("{id:guid}/delete")]
    [HasPermission(Permissions.News.Delete)]
    [ProducesResponseType<NewsItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await news.DeleteAsync(id, request, cancellationToken));
}
