namespace Onyx.Web.Localization;

/// <summary><c>{lang:onyxlang}</c>: only configured language codes match, so <c>/fr/…</c> is a 404 rather than a fallback.</summary>
public sealed class LanguageRouteConstraint(LanguageCatalog languages) : IRouteConstraint
{
    public const string Name = "onyxlang";

    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.TryGetValue(routeKey, out var value) && languages.IsSupported(value as string);
    }
}
