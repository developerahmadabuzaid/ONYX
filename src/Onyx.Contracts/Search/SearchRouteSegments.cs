namespace Onyx.Contracts.Search;

/// <summary>Localized path segment of the site search page (architecture.md §5); reserved from CMS slugs.</summary>
public static class SearchRouteSegments
{
    public static IReadOnlyDictionary<string, string> Search { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "بحث",
        ["en"] = "search",
    };
}
