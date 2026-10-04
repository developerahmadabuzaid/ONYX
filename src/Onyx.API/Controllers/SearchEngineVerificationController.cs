using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onyx.Application.Seo;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers;

/// <summary>
/// The IndexNow key, public by protocol design, for Onyx.Web's key file. 404 while IndexNow is inactive (no key configured or
/// production indexing disabled), so nothing is verifiable before launch.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/search-engine-verification")]
public sealed class SearchEngineVerificationController(ISearchEngineNotifier notifier) : ControllerBase
{
    [HttpGet("indexnow-key")]
    [AllowAnonymous]
    [Produces("text/plain")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public ActionResult IndexNowKey() =>
        notifier.VerificationKey is { } key ? Content(key, "text/plain; charset=utf-8") : NotFound();
}
