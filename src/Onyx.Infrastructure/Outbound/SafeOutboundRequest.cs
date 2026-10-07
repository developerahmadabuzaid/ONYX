namespace Onyx.Infrastructure.Outbound;

/// <param name="RequireHttps">Refuse http URLs, including redirects that downgrade to http.</param>
/// <param name="Accept">Accept header value.</param>
/// <param name="MaxResponseBytes">Overrides the configured cap when smaller (never larger).</param>
public sealed record SafeOutboundRequest(bool RequireHttps, string Accept, int? MaxResponseBytes = null);
