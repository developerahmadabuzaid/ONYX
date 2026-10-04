using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Onyx.Web.ApiClients;

namespace Onyx.Web.Controllers;

/// <summary>
/// Same-origin proxy for public media. The API decides what is reachable (live usage, or a valid preview token); only image types
/// are relayed, and preview responses are never cached.
/// </summary>
public sealed class MediaController(IPublicContentApiClient api) : Controller
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp", "image/gif" };

    [HttpGet("/media/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] string? preview, CancellationToken cancellationToken)
    {
        if (preview is { Length: > PreviewController.MaxTokenLength })
        {
            return NotFound();
        }

        var file = await api.OpenMediaAsync(id, string.IsNullOrEmpty(preview) ? null : preview, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        Response.RegisterForDisposeAsync(file);
        var mediaType = MediaTypeHeaderValue.TryParse(file.ContentType, out var parsed) ? parsed.MediaType.Value : null;
        if (mediaType is null || !AllowedTypes.Contains(mediaType))
        {
            return NotFound();
        }

        Response.Headers.CacheControl = preview is null ? (file.CacheControl ?? "public, max-age=86400") : "no-store";
        if (preview is not null)
        {
            Response.Headers["Referrer-Policy"] = "no-referrer";
        }

        Response.Headers[HeaderNames.ContentDisposition] = "inline";
        return file.ETag is not null && EntityTagHeaderValue.TryParse(file.ETag, out var tag)
            ? File(file.Content, mediaType, lastModified: null, entityTag: tag)
            : File(file.Content, mediaType);
    }
}
