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

    /// <summary>Projects: <c>/ar/أعمالنا/{project}</c>, <c>/en/work/{project}</c>.</summary>
    public static IReadOnlyDictionary<string, string> Work { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "أعمالنا",
        ["en"] = "work",
    };

    /// <summary>Case studies live under the work section: <c>/ar/أعمالنا/دراسات-الحالة/{slug}</c>, <c>/en/work/case-studies/{slug}</c>.</summary>
    public static IReadOnlyDictionary<string, string> CaseStudies { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "دراسات-الحالة",
        ["en"] = "case-studies",
    };

    public static IReadOnlyDictionary<string, string> Insights { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "المعرفة",
        ["en"] = "insights",
    };

    /// <summary>Topics live under the insights section: <c>/ar/المعرفة/مواضيع/{topic}</c>, <c>/en/insights/topics/{topic}</c>.</summary>
    public static IReadOnlyDictionary<string, string> Topics { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "مواضيع",
        ["en"] = "topics",
    };

    public static IReadOnlyDictionary<string, string> Glossary { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "المصطلحات",
        ["en"] = "glossary",
    };

    public static IReadOnlyDictionary<string, string> Integrations { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "التكاملات",
        ["en"] = "integrations",
    };

    /// <summary>
    /// Public section listings, keyed by the API's section name, with the localized path (without language) of each listing
    /// page. Their items are live content of one type.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Sections { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["work"] = Work,
            ["case-studies"] = Join(Work, CaseStudies),
            ["insights"] = Insights,
            ["topics"] = Join(Insights, Topics),
            ["glossary"] = Glossary,
            ["integrations"] = Integrations,
        };

    /// <summary>First segments that top-level pages may never use (code routes and section segments).</summary>
    public static IReadOnlySet<string> Reserved { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "preview", "language", "consent", "media", "sitemap", "sitemaps", "search", "admin", "api", "error", "robots",
        "services", "industries", "الخدمات", "القطاعات",
        "work", "insights", "glossary", "integrations", "أعمالنا", "المعرفة", "المصطلحات", "التكاملات",

        // Stage 8 conversion pages (Leads.LeadRouteSegments).
        "start-a-project", "book-a-consultation", "contact", "manage-booking", "ابدأ-مشروعك", "احجز-استشارة", "تواصل-معنا", "إدارة-الحجز",
    };

    private static Dictionary<string, string> Join(IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second) =>
        first.ToDictionary(pair => pair.Key, pair => $"{pair.Value}/{second[pair.Key]}", StringComparer.Ordinal);
}
