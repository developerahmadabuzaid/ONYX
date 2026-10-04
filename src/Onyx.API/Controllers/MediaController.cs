using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Onyx.API.Http;
using Onyx.Application.Media;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers;

/// <summary>
/// Public media files: only assets used by live content (cacheable), or by the draft a valid preview token grants (not cached).
/// Unused or unpublished uploads are not reachable here.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/media")]
public sealed class MediaController(IMediaService media) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, [FromQuery] string? previewToken, CancellationToken cancellationToken)
    {
        var file = await media.OpenPublicAsync(id, previewToken, cancellationToken);
        if (file.IsFailure)
        {
            return this.Problem(file.Error!);
        }

        Response.Headers.CacheControl = previewToken is null ? "public, max-age=86400" : "no-store";
        Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("inline") { FileName = file.Value.FileName }.ToString();
        return File(file.Value.Content, file.Value.ContentType, enableRangeProcessing: false, entityTag: new EntityTagHeaderValue($"\"{file.Value.Sha256}\""), lastModified: null);
    }
}
