using System.ComponentModel.DataAnnotations;

namespace Onyx.Web.Configuration;

/// <summary>Bound from <c>Consent</c>. Changing <see cref="PolicyVersion"/> asks every visitor again.</summary>
public sealed class ConsentOptions
{
    public const string SectionName = "Consent";

    [Required]
    [RegularExpression("^[A-Za-z0-9_-]{1,20}$")]
    public string PolicyVersion { get; set; } = "1";

    [Range(1, 400)]
    public int CookieLifetimeDays { get; set; } = 180;
}
