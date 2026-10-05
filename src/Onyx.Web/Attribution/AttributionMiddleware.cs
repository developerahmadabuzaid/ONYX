using Onyx.Web.Consent;
using Onyx.Web.Localization;

namespace Onyx.Web.Attribution;

/// <summary>Records campaign landings on public page views (GET, language paths only); never on admin, API or asset requests.</summary>
public sealed class AttributionMiddleware(RequestDelegate next, LanguageCatalog languages, TimeProvider timeProvider)
{
    public Task InvokeAsync(HttpContext context, ConsentState consent)
    {
        ArgumentNullException.ThrowIfNull(context);
        var segments = context.Request.Path.Value?.Split('/', 3, StringSplitOptions.RemoveEmptyEntries);
        if (HttpMethods.IsGet(context.Request.Method) && segments is { Length: > 0 } && languages.IsSupported(segments[0]))
        {
            AttributionCookies.Track(context, consent, timeProvider.GetUtcNow());
        }

        return next(context);
    }
}
