using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Onyx.Web.Analytics;

/// <summary>
/// The GA4 tag's script and the data it may see. No personal data reaches GA4: the page location keeps only the path and
/// <c>utm_*</c> parameters (never search text, tokens or other query values), the referrer is reduced to a path (same site) or
/// an origin (other sites), and conversion events carry only their type and language. Values are JSON-encoded with HTML-
/// sensitive characters escaped, so nothing can break out of the script element.
/// </summary>
public static class AnalyticsTag
{
    public const string ScriptOrigin = "https://www.googletagmanager.com";

    /// <summary>Origins GA4 needs (Google's documented tag endpoints), added only to responses that load the tag.</summary>
    public const string ConnectOrigins = "https://*.google-analytics.com https://*.analytics.google.com https://*.googletagmanager.com";

    public const string ImageOrigins = "https://*.google-analytics.com https://*.googletagmanager.com";

    /// <summary>One-time TempData key carrying the conversion event of a just-completed workflow.</summary>
    public const string ConversionKey = "onyx.conversion";

    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.Default };

    public static string ScriptUrl(string measurementId) => $"{ScriptOrigin}/gtag/js?id={Uri.EscapeDataString(measurementId)}";

    /// <summary>The inline configuration (and optional conversion event) script body.</summary>
    public static string Script(string measurementId, string pageLocation, string? pageReferrer, string cookieDomain, string? conversion, string language)
    {
        var config = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["page_location"] = pageLocation,
            ["cookie_domain"] = cookieDomain,
            ["cookie_flags"] = "SameSite=Lax;Secure",
        };
        if (pageReferrer is not null)
        {
            config["page_referrer"] = pageReferrer;
        }

        var script = new StringBuilder()
            .Append("window.dataLayer=window.dataLayer||[];function gtag(){dataLayer.push(arguments);}gtag('js',new Date());")
            .Append("gtag('config',").Append(JsonSerializer.Serialize(measurementId, Json)).Append(',').Append(JsonSerializer.Serialize(config, Json)).Append(");");
        if (ConversionEvent.Parse(conversion) is { } conversionEvent)
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal) { ["language"] = language };
            if (conversionEvent.InquiryType is { } type)
            {
                parameters["inquiry_type"] = type;
            }

            script.Append("gtag('event',").Append(JsonSerializer.Serialize(conversionEvent.Name, Json)).Append(',').Append(JsonSerializer.Serialize(parameters, Json)).Append(");");
        }

        return script.ToString();
    }

    /// <summary>Public origin + path + only the <c>utm_*</c> parameters of the current request.</summary>
    public static string PageLocation(string origin, HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = (request.PathBase + request.Path).ToUriComponent();
        var utm = QueryHelpers.ParseQuery(request.QueryString.Value)
            .Where(pair => pair.Key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) && pair.Key.Length <= 20)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key.ToLowerInvariant())}={Uri.EscapeDataString(Cap(pair.Value.ToString(), 100))}")
            .ToList();
        return origin.TrimEnd('/') + path + (utm.Count > 0 ? "?" + string.Join('&', utm) : string.Empty);
    }

    /// <summary>Same-site referrer → its path only; other sites → their origin; none or not a URL → null.</summary>
    public static string? PageReferrer(string origin, string? referrer)
    {
        if (!Uri.TryCreate(referrer, UriKind.Absolute, out var source) || (source.Scheme != Uri.UriSchemeHttps && source.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        var site = Uri.TryCreate(origin, UriKind.Absolute, out var own) ? own : null;
        return site is not null && string.Equals(source.Host, site.Host, StringComparison.OrdinalIgnoreCase)
            ? site.GetLeftPart(UriPartial.Authority) + source.AbsolutePath
            : source.GetLeftPart(UriPartial.Authority) + "/";
    }

    /// <summary>GA4 first-party cookie names (<c>_ga</c>, <c>_ga_&lt;stream&gt;</c>).</summary>
    public static bool IsAnalyticsCookie(string name) => name == "_ga" || name.StartsWith("_ga_", StringComparison.Ordinal);

    private static string Cap(string value, int length) => value.Length > length ? value[..length] : value;
}
