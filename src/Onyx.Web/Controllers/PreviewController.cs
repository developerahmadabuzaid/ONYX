using Microsoft.AspNetCore.Mvc;
using Onyx.Web.ApiClients;
using Onyx.Web.Content;
using Onyx.Web.Localization;
using Onyx.Web.Security;

namespace Onyx.Web.Controllers;

/// <summary>
/// Draft preview by temporary token. The page is anonymous (no admin session is used or exposed), never indexed, never cached and
/// sends no referrer, so the token cannot leak to other sites or caches.
/// </summary>
[SensitivePage]
[Analytics.NoAnalytics]
public sealed class PreviewController(IPublicContentApiClient api, SeoContext seo) : Controller
{
    public const int MaxTokenLength = 128;

    [HttpGet("/{lang:onyxlang}/preview")]
    public async Task<IActionResult> Show(string lang, [FromQuery] string? token, CancellationToken cancellationToken)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
        seo.NoIndex = true;
        seo.NoFollow = true;
        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            return NotFound();
        }

        var result = await api.GetPreviewAsync(token, cancellationToken);
        if (result.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        if (!result.IsSuccess || result.Value is null)
        {
            return NotFound();
        }

        var preview = result.Value;
        if (preview.Content.LanguageCode != lang)
        {
            return LocalRedirect($"/{preview.Content.LanguageCode}/preview?token={Uri.EscapeDataString(token)}");
        }

        seo.Title = preview.Content.Seo.Title;
        return View(new PreviewViewModel(new PageViewModel(preview.Content, token), preview.SeoPreview, preview.ExpiresAtUtc));
    }
}
