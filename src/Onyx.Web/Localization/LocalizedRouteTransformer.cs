using Microsoft.AspNetCore.Mvc.Routing;
using Onyx.Web.Content.SitePages;

namespace Onyx.Web.Localization;

/// <summary>
/// Maps <c>/{lang}/{localized path}</c> to a code-owned site page (exact path match), then <c>/{lang}/{localized segment}/{**slug}</c>
/// to a registered code route; every other path is resolved as CMS content by <c>PageController</c> (live page, 301, 410 or 404,
/// decided by the API).
/// </summary>
public sealed class LocalizedRouteTransformer(LocalizedRouteTable routes, SitePageCatalog sitePages) : DynamicRouteValueTransformer
{
    public override ValueTask<RouteValueDictionary> TransformAsync(HttpContext httpContext, RouteValueDictionary values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var language = values["lang"] as string;
        var path = values["path"] as string;
        if (string.IsNullOrEmpty(language) || string.IsNullOrEmpty(path))
        {
            return ValueTask.FromResult<RouteValueDictionary>(null!);
        }

        if (sitePages.TryMatch(language, path, out var page))
        {
            return ValueTask.FromResult(new RouteValueDictionary
            {
                ["lang"] = language,
                ["controller"] = "SitePage",
                ["action"] = "Show",
                ["page"] = page.Key,
            });
        }

        var parts = path.Split('/', 2);
        if (routes.TryMatch(language, parts[0], out var route))
        {
            return ValueTask.FromResult(new RouteValueDictionary
            {
                ["lang"] = language,
                ["controller"] = route.Controller,
                ["action"] = route.Action,
                ["slug"] = parts.Length > 1 ? parts[1] : null,
            });
        }

        return ValueTask.FromResult(new RouteValueDictionary
        {
            ["lang"] = language,
            ["controller"] = "Page",
            ["action"] = "Show",
            ["path"] = path,
        });
    }
}
