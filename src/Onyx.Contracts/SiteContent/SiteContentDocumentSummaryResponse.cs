namespace Onyx.Contracts.SiteContent;

/// <summary>One editable document in the admin list.</summary>
public sealed record SiteContentDocumentSummaryResponse(
    string Key,
    bool HasDraft,
    bool IsPublished,
    DateTimeOffset? DraftUpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    int PublishedRevision);
