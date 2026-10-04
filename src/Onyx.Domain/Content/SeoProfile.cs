namespace Onyx.Domain.Content;

/// <summary>
/// Per-language SEO settings. Arabic and English strategies are independent: each language has its own keywords, intent,
/// metadata and market. Holds no metrics; performance data comes only from connected providers, with provenance.
/// </summary>
public sealed record SeoProfile(
    string? PrimaryKeyword = null,
    IReadOnlyList<string>? SecondaryKeywords = null,
    SearchIntent SearchIntent = SearchIntent.Unspecified,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? CanonicalPath = null,
    bool Index = true,
    bool Follow = true,
    StructuredDataTypes StructuredData = StructuredDataTypes.WebPage | StructuredDataTypes.BreadcrumbList,
    string? OpenGraphTitle = null,
    string? OpenGraphDescription = null,
    Guid? OpenGraphImageId = null,
    string? TargetMarket = null,
    SeoStatus Status = SeoStatus.NotStarted,
    string? Notes = null)
{
    public const int KeywordMaxLength = 150;
    public const int MaxSecondaryKeywords = 20;
    public const int MetaTitleMaxLength = 200;
    public const int MetaDescriptionMaxLength = 400;
    public const int OpenGraphTitleMaxLength = 200;
    public const int OpenGraphDescriptionMaxLength = 400;
    public const int NotesMaxLength = 2_000;

    public static SeoProfile Empty { get; } = new();
}
