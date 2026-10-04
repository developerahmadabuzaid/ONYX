namespace Onyx.Domain.Intelligence;

/// <summary>Stored as text; append only.</summary>
public enum RecommendationKind
{
    PrimaryKeyword = 0,
    SecondaryKeywords = 1,
    SearchIntent = 2,
    Title = 3,
    MetaTitle = 4,
    MetaDescription = 5,
    Slug = 6,
    HeadingStructure = 7,
    Faq = 8,
    InternalLink = 9,
    StructuredData = 10,
    ContentImprovement = 11,
    Cannibalization = 12,
    PublishingTime = 13,
    Freshness = 14,
    ContentGap = 15,
    Consolidation = 16,
    OrphanContent = 17,
    BrokenLink = 18,
}
