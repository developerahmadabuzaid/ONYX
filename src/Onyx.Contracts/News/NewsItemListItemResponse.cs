namespace Onyx.Contracts.News;

public sealed record NewsItemListItemResponse(
    Guid Id,
    string OriginalTitle,
    string Publisher,
    string Status,
    string? HoldReason,
    Guid CategoryId,
    DateTimeOffset? SourcePublishedAtUtc,
    DateTimeOffset IngestedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    bool AutoPublished,
    bool Indexable,
    IReadOnlyList<string> SummaryLanguages);
