namespace Onyx.Contracts.News;

/// <summary>A news source for administrators. <see cref="LastError"/> is a safe code and description only.</summary>
public sealed record NewsSourceResponse(
    Guid Id,
    string Name,
    string HomepageUrl,
    string FeedUrl,
    string FeedFormat,
    string LanguageCode,
    Guid DefaultCategoryId,
    string Status,
    string? LicensingNote,
    bool AutoPublish,
    int FetchIntervalMinutes,
    DateTimeOffset? NextFetchAtUtc,
    DateTimeOffset? LastFetchedAtUtc,
    DateTimeOffset? LastSucceededAtUtc,
    string? LastError,
    int ConsecutiveFailures,
    uint Version);
