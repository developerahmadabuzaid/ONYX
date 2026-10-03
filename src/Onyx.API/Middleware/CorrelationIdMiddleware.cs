using System.Text.RegularExpressions;
using Serilog.Context;

namespace Onyx.API.Middleware;

/// <summary>Accepts a well-formed inbound X-Correlation-ID (e.g. from Onyx.Web) or creates one; echoes it and adds it to every log event.</summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "Onyx.CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var inbound = context.Request.Headers[HeaderName].ToString();
        var correlationId = SafeCorrelationId().IsMatch(inbound) ? inbound : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    // Rejects header injection and log forging: short, URL-safe identifiers only.
    [GeneratedRegex("^[A-Za-z0-9._-]{8,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCorrelationId();
}
