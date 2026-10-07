namespace Onyx.Contracts.News;

/// <summary>An item for editors: source metadata (attribution, original link and date) and Onyx's texts per language.</summary>
public sealed record NewsItemResponse(
    Guid Id,
    Guid SourceId,
    string Publisher,
    string OriginalUrl,
    string OriginalTitle,
    string SourceLanguageCode,
    DateTimeOffset? SourcePublishedAtUtc,
    DateTimeOffset IngestedAtUtc,
    Guid CategoryId,
    Guid? SimilarToItemId,
    string Status,
    string? HoldReason,
    string? SummaryOrigin,
    string? SummaryProvider,
    bool AutoPublished,
    bool Indexable,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? FirstPublishedAtUtc,
    IReadOnlyList<NewsTranslationDto> Translations,
    uint Version);
