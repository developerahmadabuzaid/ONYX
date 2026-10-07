using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Http;
using Onyx.Application.News;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.News;

namespace Onyx.API.Controllers;

/// <summary>
/// The public News Hub (architecture.md §17): published items only, each with its attribution (publisher, original link, the
/// source's time when known, Onyx's time). Read-only and anonymous; held, hidden, unpublished and deleted items are never
/// returned by any endpoint.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/news")]
[AllowAnonymous]
public sealed class NewsController(PublicNewsService news) : ControllerBase
{
    /// <summary>The hub: the latest published items (20 per page) and the categories.</summary>
    [HttpGet]
    [ProducesResponseType<PublicNewsListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Hub([FromQuery] string language, [FromQuery] int page = 1, CancellationToken cancellationToken = default) =>
        this.OkOrProblem(await news.GetHubAsync(language, page, cancellationToken));

    /// <summary>A slug directly below the hub: a category page, a published item, or neither (both fields null).</summary>
    [HttpGet("resolve")]
    [ProducesResponseType<NewsResolveResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Resolve([FromQuery] string language, [FromQuery] string slug, [FromQuery] int page = 1, CancellationToken cancellationToken = default) =>
        this.OkOrProblem(await news.ResolveAsync(language, slug, page, cancellationToken));

    /// <summary>The homepage's latest items (at most four).</summary>
    [HttpGet("latest")]
    [ProducesResponseType<IReadOnlyList<PublicNewsItemSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicNewsItemSummaryResponse>>> Latest(
        [FromQuery] string language, [FromQuery] int count = PublicNewsService.MaxLatest, CancellationToken cancellationToken = default) =>
        Ok(await news.GetLatestAsync(language, count, cancellationToken));

    /// <summary>Sitemap entries: the hub and categories with published items, and items marked indexable.</summary>
    [HttpGet("sitemap")]
    [ProducesResponseType<IReadOnlyList<SitemapEntryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SitemapEntryResponse>>> Sitemap([FromQuery] string language, CancellationToken cancellationToken) =>
        Ok(await news.GetSitemapAsync(language, cancellationToken));
}
