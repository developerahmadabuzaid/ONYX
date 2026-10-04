using Microsoft.AspNetCore.Mvc.Routing;

namespace Onyx.Web.Localization;

/// <summary>
/// Maps <c>/{lang}/{localized segment}/{**slug}</c> to a registered code route; every other path is resolved as CMS content by
/// <c>PageController</c> (live page, 301, 410 or 404, decided by the API).
/// </summary>
public sealed class LocalizedRouteTransformer(LocalizedRouteTable routes) : DynamicRouteValueTransformer
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
