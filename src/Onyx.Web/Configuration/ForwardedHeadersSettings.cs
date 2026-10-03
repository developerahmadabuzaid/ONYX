namespace Onyx.Web.Configuration;

/// <summary>
/// Reverse proxies in front of Onyx.Web whose X-Forwarded-For / X-Forwarded-Proto headers are trusted. Empty means only
/// loopback is trusted. The resolved visitor address is what Onyx.Web forwards to Onyx.API.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public IReadOnlyList<string> KnownProxies { get; set; } = [];

    /// <summary>CIDR notation, e.g. "10.0.0.0/8".</summary>
    public IReadOnlyList<string> KnownNetworks { get; set; } = [];
}
