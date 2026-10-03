using System.ComponentModel.DataAnnotations;

namespace Onyx.Web.Configuration;

/// <summary>Bound from <c>Site</c>.</summary>
public sealed class SiteOptions
{
    public const string SectionName = "Site";

    /// <summary>Absolute origin used for canonical and hreflang URLs.</summary>
    [Required]
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>False until launch: every page carries <c>noindex, nofollow</c> so nothing is indexed prematurely.</summary>
    public bool AllowIndexing { get; set; }
}
