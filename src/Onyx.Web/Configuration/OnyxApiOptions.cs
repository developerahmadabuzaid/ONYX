using System.ComponentModel.DataAnnotations;

namespace Onyx.Web.Configuration;

/// <summary>Bound from <c>OnyxApi</c>. Onyx.Web reaches the backend only through this HTTPS base address.</summary>
public sealed class OnyxApiOptions
{
    public const string SectionName = "OnyxApi";

    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    [Range(2, 120)]
    public int TimeoutSeconds { get; set; } = 30;
}
