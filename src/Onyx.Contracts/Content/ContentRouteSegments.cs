namespace Onyx.Contracts.Content;

/// <summary>
/// Localized first URL segments of content sections (docs/architecture.md §5, CLAUDE.md §7). Shared by Onyx.API (which builds
/// paths, canonicals and sitemaps) and Onyx.Web (which routes them), so both always agree.
/// </summary>
public static class ContentRouteSegments
{
    public static IReadOnlyDictionary<string, string> Services { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "الخدمات",
        ["en"] = "services",
    };

    public static IReadOnlyDictionary<string, string> Industries { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "القطاعات",
        ["en"] = "industries",
    };

    /// <summary>First segments that top-level pages may never use (code routes and section segments).</summary>
    public static IReadOnlySet<string> Reserved { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "preview", "language", "consent", "media", "sitemap", "sitemaps", "search", "admin", "api", "error", "robots",
        "services", "industries", "الخدمات", "القطاعات",
    };
}
