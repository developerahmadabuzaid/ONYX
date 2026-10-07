namespace Onyx.Contracts.News;

/// <summary>
/// The News Hub section (architecture.md §5): <c>/ar/آخر-الأخبار/</c>, <c>/en/news/</c>, with categories and items one level
/// below. Category and item slugs share that level, so they are kept distinct.
/// </summary>
public static class NewsRouteSegments
{
    public static IReadOnlyDictionary<string, string> Hub { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "آخر-الأخبار",
        ["en"] = "news",
    };
}
