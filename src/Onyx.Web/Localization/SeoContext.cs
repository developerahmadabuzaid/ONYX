namespace Onyx.Web.Localization;

/// <summary>
/// Per-request SEO metadata filled by the page and rendered by the public layout (canonical, hreflang, robots, Open Graph, JSON-LD).
/// All paths are root-relative and already percent-encoded.
/// </summary>
public sealed class SeoContext
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    /// <summary>Root-relative canonical path of the current page.</summary>
    public string? CanonicalPath { get; set; }

    /// <summary>Language → root-relative path of the equivalent page; drives hreflang and the language switcher.</summary>
    public IDictionary<string, string> Alternates { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Explicit x-default target; when unset, the default language's alternate is used.</summary>
    public string? XDefaultPath { get; set; }

    /// <summary>Forces <c>noindex</c> (search, tool results, account pages, previews) even after launch.</summary>
    public bool NoIndex { get; set; }

    /// <summary>Asks crawlers not to follow links on the page.</summary>
    public bool NoFollow { get; set; }

    public string? OpenGraphTitle { get; set; }

    public string? OpenGraphDescription { get; set; }

    /// <summary>Root-relative path of the Open Graph image.</summary>
    public string? OpenGraphImagePath { get; set; }

    public string? OpenGraphImageAlt { get; set; }

    /// <summary>Serialized, HTML-safe JSON-LD document (see <c>StructuredDataBuilder</c>); null when the page declares none.</summary>
    public string? StructuredDataJson { get; set; }
}
