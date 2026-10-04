using Microsoft.AspNetCore.Mvc;
using Onyx.Web.ApiClients;

namespace Onyx.Web.Controllers;

/// <summary>
/// The IndexNow key file (its location is sent with every submission). 404 until IndexNow is configured and production indexing
/// is enabled; the key itself comes from Onyx.API, so it is configured in one place.
/// </summary>
public sealed class SearchEngineVerificationController(IPublicContentApiClient api) : Controller
{
    [HttpGet("/indexnow-key.txt")]
    public async Task<IActionResult> IndexNowKey(CancellationToken cancellationToken)
    {
        var key = await api.GetIndexNowKeyAsync(cancellationToken);
        if (key is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=3600";
        return Content(key, "text/plain; charset=utf-8");
    }
}
