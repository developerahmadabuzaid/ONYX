using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record ContentDetailResponse(
    Guid Id,
    string Type,
    Guid? ParentId,
    string? Key,
    string Presentation,
    int SortOrder,
    string Status,
    bool IsLive,
    bool HasUnpublishedChanges,
    int CurrentRevisionNumber,
    Guid? PrimaryCtaId,
    Guid? FeaturedImageId,
    string? ReviewNote,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? ScheduledPublishAtUtc,
    DateTimeOffset? ScheduledUnpublishAtUtc,
    string? ScheduleTimeZone,
    DateTimeOffset? FirstPublishedAtUtc,
    DateTimeOffset? LastPublishedAtUtc,
    string OfflineResolution,
    Guid? OfflineRedirectToId,
    IReadOnlyList<ContentTranslationResponse> Translations,
    IReadOnlyList<ContentPathResponse> Paths,
    uint Version,
    string? InsightKind = null,
    string Origin = "Human",
    Guid? AuthorId = null,
    ProofVerificationResponse? ProofVerification = null,
    int? FeaturedSlot = null);
