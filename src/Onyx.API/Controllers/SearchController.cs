using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Onyx.API.Configuration;
using Onyx.API.Http;
using Onyx.Application.Search;
using Onyx.Contracts.Common;
using Onyx.Contracts.Search;

namespace Onyx.API.Controllers;

/// <summary>
/// Public site search over published content only (architecture.md §18). Anonymous, rate limited per client IP, query capped at
/// 100 characters, results capped per group, never cached and never logged with the query.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/search")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Search)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SearchController(SiteSearchQueryService search) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SiteSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Search([FromQuery] string language, [FromQuery] string? q, CancellationToken cancellationToken) =>
        this.OkOrProblem(await search.SearchAsync(language, q, cancellationToken));
}
