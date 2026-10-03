using System.Globalization;

namespace Onyx.Web.ApiClients.Handlers;

/// <summary>
/// Adds the correlation id, the visitor's language and the real client IP to every API call. The API trusts
/// <c>X-Forwarded-For</c> only from configured proxies (Onyx.Web), so its rate limits apply per visitor, not per Web server.
/// </summary>
public sealed class RequestContextHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    public const string CorrelationItemKey = "Onyx.CorrelationId";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var httpContext = httpContextAccessor.HttpContext;

        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.ParseAdd(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

        if (httpContext is not null)
        {
            if (httpContext.Items[CorrelationItemKey] is not string correlationId)
            {
                correlationId = Guid.NewGuid().ToString("N");
                httpContext.Items[CorrelationItemKey] = correlationId;
            }

            request.Headers.Remove("X-Correlation-ID");
            request.Headers.Add("X-Correlation-ID", correlationId);

            if (httpContext.Connection.RemoteIpAddress is { } clientIp)
            {
                request.Headers.Remove("X-Forwarded-For");
                request.Headers.Add("X-Forwarded-For", clientIp.ToString());
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
