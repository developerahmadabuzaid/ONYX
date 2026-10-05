namespace Onyx.Web.Areas.Admin.Cms;

/// <summary>Option values offered by the admin forms. They mirror the API's enums by name; the API validates every value.</summary>
public static class CmsOptions
{
    public static IReadOnlyList<string> Languages { get; } = ["ar", "en"];

    public static IReadOnlyList<string> ContentTypes { get; } =
        ["ServicePillar", "Service", "Industry", "Page", "Project", "CaseStudy", "Insight", "Topic", "GlossaryTerm", "Integration"];

    /// <summary>Types that are public proof and need a recorded verification before publishing.</summary>
    public static IReadOnlySet<string> ProofTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { "Project", "CaseStudy", "Integration" };

    public static IReadOnlyList<string> InsightKinds { get; } = ["Article", "Guide"];

    public static IReadOnlyList<string> InsightOrigins { get; } = ["Human", "AiAssisted"];

    public static IReadOnlyList<string> ProofItemKinds { get; } = ["Capability", "Metric", "Testimonial"];

    public static IReadOnlyList<string> ClientPermissions { get; } = ["NotApplicable", "Granted"];

    public static IReadOnlyList<string> Statuses { get; } = ["Draft", "PendingReview", "Approved", "Scheduled", "Published", "Archived"];

    public static IReadOnlyList<string> Presentations { get; } = ["Page", "TaxonomyOnly"];

    public static IReadOnlyList<string> OfflineResolutions { get; } = ["PendingDecision", "Gone", "RedirectToContent"];

    public static IReadOnlyList<string> SearchIntents { get; } = ["Unspecified", "Informational", "Commercial", "Transactional", "Navigational"];

    public static IReadOnlyList<string> SeoStatuses { get; } = ["NotStarted", "InProgress", "Ready", "NeedsReview"];

    public static IReadOnlyList<string> StructuredDataTypes { get; } = ["WebPage", "BreadcrumbList", "Service", "FaqPage", "Article"];

    public static IReadOnlyList<string> RelationKinds { get; } = ["Related", "ServiceIndustry"];

    public static IReadOnlyList<string> ClusterRoles { get; } = ["Pillar", "Supporting"];

    public static IReadOnlyList<string> RedirectKinds { get; } = ["Permanent", "Gone"];

    public static IReadOnlyList<string> CtaActionTypes { get; } =
        ["StartProject", "BookConsultation", "Contact", "RequestConsultation", "AnalyzeWebsite", "CheckSecurityPosture", "DiscoverOpportunities", "Url"];

    public static IReadOnlyList<string> KnowledgeCategories { get; } =
        ["CompanyInformation", "Service", "Capability", "ProjectFact", "BrandGuidance", "WritingRule", "LocationAndContact", "VerifiedClaim"];

    public static IReadOnlyList<string> KnowledgeStatuses { get; } = ["Draft", "Approved", "Retired"];

    public static IReadOnlyList<string> KeywordStatuses { get; } = ["Planned", "Targeted", "Monitoring", "Retired"];

    public static IReadOnlyList<string> RecommendationKinds { get; } =
    [
        "PrimaryKeyword", "SecondaryKeywords", "SearchIntent", "Title", "MetaTitle", "MetaDescription", "Slug", "HeadingStructure", "Faq",
        "InternalLink", "StructuredData", "ContentImprovement", "Cannibalization", "PublishingTime", "Freshness", "ContentGap", "Consolidation",
        "OrphanContent", "BrokenLink",
    ];

    public static IReadOnlyList<string> RecommendationStatuses { get; } = ["Proposed", "Approved", "Rejected", "Applied", "Superseded"];

    public static IReadOnlyList<string> CalendarStatuses { get; } = ["Proposed", "Accepted", "InPreparation", "Completed", "Rejected"];

    public static IReadOnlyList<string> InformationOutcomes { get; } = ["Answered", "Dismissed"];
}
