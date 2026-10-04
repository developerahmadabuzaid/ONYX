using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record ContentSummaryResponse(
    Guid Id,
    string Type,
    Guid? ParentId,
    string? Key,
    string Presentation,
    string Status,
    bool IsLive,
    bool HasUnpublishedChanges,
    IReadOnlyDictionary<string, string> Titles,
    string OfflineResolution,
    DateTimeOffset? LastPublishedAtUtc,
    DateTimeOffset? ScheduledPublishAtUtc,
    DateTimeOffset? ScheduledUnpublishAtUtc,
    DateTimeOffset? UpdatedAtUtc);
