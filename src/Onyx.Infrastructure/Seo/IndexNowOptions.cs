using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.Seo;

/// <summary>
/// Bound from <c>IndexNow</c>. The key is public by design (it is served in the key file) but is kept in configuration so it
/// can be rotated per environment. Empty key = inactive.
/// </summary>
public sealed class IndexNowOptions
{
    public const string SectionName = "IndexNow";

    public string Key { get; set; } = string.Empty;

    /// <summary>The shared IndexNow endpoint; submissions are forwarded to every participating search engine.</summary>
    [Required]
    public string Endpoint { get; set; } = "https://api.indexnow.org/indexnow";

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 15;
}
