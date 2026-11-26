namespace Onyx.Contracts.SiteContent;

/// <summary>A document with its working copy and live version (JSON objects, both languages). Absent documents come back empty.</summary>
public sealed record SiteContentDocumentResponse(
    string Key,
    string? DraftJson,
    string? PublishedJson,
    DateTimeOffset? DraftUpdatedAtUtc,
    Guid? DraftUpdatedById,
    DateTimeOffset? PublishedAtUtc,
    Guid? PublishedById,
    int PublishedRevision,
    uint Version);
