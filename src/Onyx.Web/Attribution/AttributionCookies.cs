using System.Buffers.Text;
using System.Text.Json;
using Onyx.Contracts.Leads;
using Onyx.Web.Consent;

namespace Onyx.Web.Attribution;

/// <summary>
/// Consent-aware marketing attribution (architecture.md §14). Only with Analytics consent does the site keep persistent
/// first-touch and session last-touch cookies (HttpOnly, never readable by scripts); without consent they are deleted and a
/// submission carries only the context of the page it was sent from. Values are bounded and re-validated by the API.
/// </summary>
public static class AttributionCookies
{
    public const string FirstTouchCookie = "onyx.ft";
    public const string LastTouchCookie = "onyx.lt";
    public const int FirstTouchLifetimeDays = 90;
    public const int FieldMaxLength = 150;
    public const int PathMaxLength = 500;

    private static readonly string[] UtmKeys = ["utm_source", "utm_medium", "utm_campaign", "utm_content", "utm_term"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The touch this request represents (campaign tags or an external referrer), or null for internal navigation.</summary>
    public static TouchPointDto? CurrentTouch(HttpRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        var hasCampaign = UtmKeys.Any(key => !string.IsNullOrWhiteSpace(request.Query[key]));
        var referrer = ExternalReferrer(request);
        if (!hasCampaign && referrer is null)
        {
            return null;
        }

        return new TouchPointDto(
            Bound(request.Path.Value, PathMaxLength), referrer, Tag(request, "utm_source"), Tag(request, "utm_medium"), Tag(request, "utm_campaign"),
            Tag(request, "utm_content"), Tag(request, "utm_term"), now);
    }

    /// <summary>Records the touch when analytics is allowed; otherwise removes any attribution cookies.</summary>
    public static void Track(HttpContext context, ConsentState consent, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(consent);
        if (!consent.AnalyticsAllowed)
        {
            foreach (var name in new[] { FirstTouchCookie, LastTouchCookie }.Where(context.Request.Cookies.ContainsKey))
            {
                context.Response.Cookies.Delete(name, Options(null));
            }

            return;
        }

        if (CurrentTouch(context.Request, now) is not { } touch)
        {
            return;
        }

        var value = Encode(touch);
        context.Response.Cookies.Append(LastTouchCookie, value, Options(null));
        if (!context.Request.Cookies.ContainsKey(FirstTouchCookie))
        {
            context.Response.Cookies.Append(FirstTouchCookie, value, Options(TimeSpan.FromDays(FirstTouchLifetimeDays)));
        }
    }

    /// <summary>First and last touch from the cookies; both null without analytics consent.</summary>
    public static (TouchPointDto? First, TouchPointDto? Last) Read(HttpRequest request, ConsentState consent)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(consent);
        return consent.AnalyticsAllowed
            ? (Decode(request.Cookies[FirstTouchCookie]), Decode(request.Cookies[LastTouchCookie]))
            : (null, null);
    }

    public static string Encode(TouchPointDto touch) => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(touch, Json));

    public static TouchPointDto? Decode(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 2_000)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TouchPointDto>(Base64Url.DecodeFromChars(value), Json);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }

    /// <summary>The GA client id from the <c>_ga</c> cookie (<c>GA1.1.123.456</c> → <c>123.456</c>), only with analytics consent.</summary>
    public static string? AnalyticsClientId(HttpRequest request, ConsentState consent)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(consent);
        var parts = consent.AnalyticsAllowed ? request.Cookies["_ga"]?.Split('.') : null;
        return parts is { Length: 4 } && parts[2].All(char.IsAsciiDigit) && parts[3].All(char.IsAsciiDigit) ? $"{parts[2]}.{parts[3]}" : null;
    }

    /// <summary>A coarse device class from the User-Agent; no fingerprinting.</summary>
    public static string DeviceClass(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var agent = request.Headers.UserAgent.ToString();
        if (agent.Contains("iPad", StringComparison.OrdinalIgnoreCase) || agent.Contains("Tablet", StringComparison.OrdinalIgnoreCase))
        {
            return "tablet";
        }

        return agent.Contains("Mobi", StringComparison.OrdinalIgnoreCase) ? "mobile" : "desktop";
    }

    private static string? ExternalReferrer(HttpRequest request)
    {
        var header = request.Headers.Referer.ToString();
        return Uri.TryCreate(header, UriKind.Absolute, out var referrer) && referrer.Scheme is "http" or "https"
            && !string.Equals(referrer.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase)
            ? referrer.GetLeftPart(UriPartial.Authority)
            : null;
    }

    private static string? Tag(HttpRequest request, string key) => Bound(request.Query[key].ToString(), FieldMaxLength);

    private static string? Bound(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static CookieOptions Options(TimeSpan? lifetime) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = false,
        MaxAge = lifetime,
    };
}
