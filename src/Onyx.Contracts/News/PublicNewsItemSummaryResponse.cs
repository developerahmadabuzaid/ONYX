namespace Onyx.Contracts.News;

/// <summary>A published item in a listing: Onyx's headline and summary, the publisher, and both dates (the source's and Onyx's).</summary>
public sealed record PublicNewsItemSummaryResponse(
    string Title,
    string Summary,
    string Path,
    string Publisher,
    string? CategoryName,
    DateTimeOffset? SourcePublishedAtUtc,
    DateTimeOffset PublishedAtUtc);
