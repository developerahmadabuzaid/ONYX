namespace Onyx.Web.Localization;

/// <summary>Per-request SEO metadata filled by the page and rendered by the public layout (canonical, hreflang, robots).</summary>
public sealed class SeoContext
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    /// <summary>Root-relative canonical path of the current page.</summary>
    public string? CanonicalPath { get; set; }

    /// <summary>Language → root-relative path of the equivalent page; drives hreflang and the language switcher.</summary>
    public IDictionary<string, string> Alternates { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Forces <c>noindex</c> (search, tool results, account pages) even after launch.</summary>
    public bool NoIndex { get; set; }
}
