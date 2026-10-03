using System.ComponentModel.DataAnnotations;

namespace Onyx.API.Configuration;

public sealed class ApiSecurityOptions
{
    public const string SectionName = "ApiSecurity";

    /// <summary>Global request body cap. Endpoints that need more (media upload) opt in explicitly later.</summary>
    [Range(1_024, 104_857_600)]
    public long MaxRequestBodySizeBytes { get; set; } = 1_048_576;

    [Range(1, 730)]
    public int HstsMaxAgeDays { get; set; } = 365;
}
