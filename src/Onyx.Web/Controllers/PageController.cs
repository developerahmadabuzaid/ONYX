using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Onyx.Web.ApiClients;
using Onyx.Web.Configuration;
using Onyx.Web.Content;
using Onyx.Web.Localization;
using Onyx.Web.Resources;

namespace Onyx.Web.Controllers;

/// <summary>
/// Serves CMS content at its localized path. Reached only through <see cref="LocalizedRouteTransformer"/>; the API decides whether
/// the path is live (200), moved (301 to the current path, never a chain), gone (410) or unknown (404).
/// </summary>
public sealed class PageController(IPublicContentApiClient api, SeoContext seo, IOptions<SiteOptions> site, IStringLocalizer<SharedResource> localizer) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Show(string lang, string path, CancellationToken cancellationToken)
    {
        // One URL per page: "/en/x/" answers 301 to "/en/x" instead of serving a duplicate.
        if (Request.Path.Value is { Length: > 1 } requested && requested.EndsWith('/'))
        {
            return LocalRedirectPermanent(SitePaths.Encode(requested.TrimEnd('/')) + Request.QueryString);
        }

        var result = await api.ResolveAsync(lang, path, cancellationToken);
        if (result.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        if (!result.IsSuccess || result.Value is null)
        {
            return result.StatusCode == HttpStatusCode.NotFound ? NotFound() : StatusCode(StatusCodes.Status502BadGateway);
        }

        var resolved = result.Value;
        switch (resolved.Kind)
        {
            case "Content" when resolved.Content is not null && resolved.Content.LanguageCode == lang:
                seo.Apply(resolved.Content, site.Value.PublicBaseUrl, localizer["Site.Name"].Value);
                return View(new PageViewModel(resolved.Content));

            // Redirect targets come from the API as site paths in this site's own languages; LocalRedirect rejects anything else.
            case "Redirect" when resolved.RedirectPath is { Length: > 1 } target && target.StartsWith('/') && !target.StartsWith("//", StringComparison.Ordinal):
                return LocalRedirectPermanent(SitePaths.Encode(target));

            case "Gone":
                return StatusCode(StatusCodes.Status410Gone);

            default:
                return NotFound();
        }
    }
}
