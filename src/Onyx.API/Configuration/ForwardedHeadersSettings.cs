namespace Onyx.API.Configuration;

/// <summary>
/// Proxies whose X-Forwarded-For / X-Forwarded-Proto headers are trusted (reverse proxy, Onyx.Web).
/// Empty means only loopback is trusted, so a client can never spoof its IP for rate limiting or audit records.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public IReadOnlyList<string> KnownProxies { get; set; } = [];

    /// <summary>CIDR notation, e.g. "10.0.0.0/8".</summary>
    public IReadOnlyList<string> KnownNetworks { get; set; } = [];
}
