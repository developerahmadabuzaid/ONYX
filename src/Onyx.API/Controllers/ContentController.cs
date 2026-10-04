using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Onyx.API.Configuration;
using Onyx.API.Http;
using Onyx.Application.Content;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.API.Controllers;

/// <summary>Public, read-only content: live pages, URL resolution (redirects/gone), sitemap entries and token-gated draft previews.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/content")]
public sealed class ContentController(IPublicContentService publicContent, IContentPreviewService previews) : ControllerBase
{
    /// <summary>Resolves a site path (without the language prefix) to live content, a permanent redirect, 410 Gone or 404.</summary>
    [HttpGet("resolve")]
    [AllowAnonymous]
    [ProducesResponseType<ContentResolveResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ContentResolveResponse>> Resolve([FromQuery] string language, [FromQuery] string path, CancellationToken cancellationToken) =>
        Ok(await publicContent.ResolveAsync(language ?? string.Empty, path ?? string.Empty, cancellationToken));

    [HttpGet("sitemap")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<SitemapEntryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SitemapEntryResponse>>> Sitemap([FromQuery] string language, CancellationToken cancellationToken) =>
        Ok(await publicContent.GetSitemapAsync(language ?? string.Empty, cancellationToken));

    /// <summary>Draft preview. The token is the only credential; responses are never cached.</summary>
    [HttpGet("preview")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType<ContentPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Preview([FromQuery] string token, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return this.OkOrProblem(await previews.GetPreviewAsync(token ?? string.Empty, cancellationToken));
    }
}
