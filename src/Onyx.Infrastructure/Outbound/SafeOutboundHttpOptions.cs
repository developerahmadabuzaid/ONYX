using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.Outbound;

/// <summary>Bound from <c>SafeOutboundHttp</c>. Limits for every visitor- or admin-supplied external URL (CLAUDE.md §8).</summary>
public sealed class SafeOutboundHttpOptions
{
    public const string SectionName = "SafeOutboundHttp";

    /// <summary>Whole request, redirects included.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 20;

    [Range(1, 60)]
    public int ConnectTimeoutSeconds { get; set; } = 10;

    /// <summary>Largest (decompressed) response body read; larger responses are refused.</summary>
    [Range(1_024, 20 * 1_024 * 1_024)]
    public int MaxResponseBytes { get; set; } = 2 * 1_024 * 1_024;

    [Range(0, 10)]
    public int MaxRedirects { get; set; } = 3;

    /// <summary>Identifiable User-Agent; its product token (before '/') is what robots.txt groups are matched against.</summary>
    [Required]
    [RegularExpression(@"^[A-Za-z0-9\-]{3,40}/[0-9.]{1,10}( \([^()\r\n]{1,150}\))?$")]
    public string UserAgent { get; set; } = "OnyxVanguard-NewsReader/1.0";
}
