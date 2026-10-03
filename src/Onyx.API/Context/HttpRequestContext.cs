using Onyx.API.Middleware;
using Onyx.Application.Abstractions;

namespace Onyx.API.Context;

/// <summary>Correlation id from <see cref="CorrelationIdMiddleware"/>; client IP as resolved by the forwarded-headers middleware (trusted proxies only).</summary>
public sealed class HttpRequestContext(IHttpContextAccessor httpContextAccessor) : IRequestContext
{
    public string? CorrelationId => httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string;

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
