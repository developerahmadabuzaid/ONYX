using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onyx.Application.SiteContent;
using Onyx.Contracts.SiteContent;

namespace Onyx.API.Controllers;

/// <summary>Published site content overrides for Onyx.Web (which caches them). Drafts are never served here.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/site-content")]
public sealed class SiteContentController(ISiteContentService siteContent) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PublicSiteContentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicSiteContentResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await siteContent.GetPublishedAsync(cancellationToken));
}
