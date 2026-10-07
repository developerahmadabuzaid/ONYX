using Onyx.Domain.Content;

namespace Onyx.Application.Search;

/// <summary>
/// Result groups (architecture.md §18) and their ranking boosts. The boosts only order groups of otherwise equal relevance; they
/// are product tuning values, not business facts. News (Stage 11) is additionally ranked by freshness.
/// </summary>
public static class SearchGroups
{
    public const string Services = "Services";
    public const string Industries = "Industries";
    public const string Work = "Work";
    public const string Insights = "Insights";
    public const string News = "News";
    public const string Glossary = "Glossary";
    public const string Integrations = "Integrations";
    public const string Pages = "Pages";

    /// <summary>Content type recorded for news documents.</summary>
    public const string NewsItemType = "NewsItem";

    /// <summary>Display order of groups on the results page.</summary>
    public static IReadOnlyList<string> Ordered { get; } = [Services, Industries, Work, Insights, News, Glossary, Integrations, Pages];

    public static string For(ContentType type) => type switch
    {
        ContentType.ServicePillar or ContentType.Service => Services,
        ContentType.Industry => Industries,
        ContentType.Project or ContentType.CaseStudy => Work,
        ContentType.Insight or ContentType.Topic => Insights,
        ContentType.GlossaryTerm => Glossary,
        ContentType.Integration => Integrations,
        _ => Pages,
    };

    public static double Boost(string group) => group switch
    {
        Services => 1.3,
        Industries => 1.2,
        Work => 1.15,
        Insights or News => 1.0,
        Glossary or Integrations => 0.95,
        _ => 0.9,
    };
}
