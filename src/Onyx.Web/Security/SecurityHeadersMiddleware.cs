using System.Security.Cryptography;

namespace Onyx.Web.Security;

/// <summary>
/// Security headers for every Web response, including a per-request CSP nonce. Everything is self-hosted: no third-party
/// scripts, styles, fonts or frames, so token-bearing pages cannot leak anything to other origins. The one exception is the
/// Cloudflare Turnstile challenge, allowed only on the public form pages that render it (<see cref="AllowsTurnstileAttribute"/>),
/// and Google Analytics, allowed only on responses for visitors who accepted analytics cookies (<see cref="Analytics.AnalyticsTagFilter"/>).
/// Admin responses are never cached.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string NonceItemKey = "Onyx.CspNonce";
    public const string TurnstileItemKey = "Onyx.CspTurnstile";
    public const string AnalyticsItemKey = "Onyx.CspAnalytics";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[NonceItemKey] = nonce;
        var isAdmin = context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase);

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Form pages marked [AllowsTurnstile] may load the anti-abuse challenge (script + iframe) from its single origin.
            var turnstile = context.Items.ContainsKey(TurnstileItemKey) ? " " + Configuration.TurnstileOptions.Origin : string.Empty;

            // Consenting visitors on public pages only: Google's tag script, its collection endpoints and pixels. Never on admin.
            var analytics = !isAdmin && context.Items.ContainsKey(AnalyticsItemKey);
            var analyticsScript = analytics ? " " + Analytics.AnalyticsTag.ScriptOrigin : string.Empty;
            var analyticsConnect = analytics ? " " + Analytics.AnalyticsTag.ConnectOrigins : string.Empty;
            var analyticsImages = analytics ? " " + Analytics.AnalyticsTag.ImageOrigins : string.Empty;
            headers.ContentSecurityPolicy =
                $"default-src 'self'; script-src 'self' 'nonce-{nonce}'{turnstile}{analyticsScript}; style-src 'self'; img-src 'self' data:{analyticsImages}; font-src 'self'; " +
                $"connect-src 'self'{turnstile}{analyticsConnect}; frame-src 'self'{turnstile}; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; upgrade-insecure-requests";
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), microphone=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";

            // Sensitive pages set a stricter policy before the response starts; do not weaken it.
            if (!headers.ContainsKey("Referrer-Policy"))
            {
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            }

            if (isAdmin)
            {
                headers.CacheControl = "no-store";
            }

            headers.Remove("Server");
            headers.Remove("X-Powered-By");
            return Task.CompletedTask;
        });

        return next(context);
    }
}
