namespace Onyx.Contracts.News;

/// <summary>Creates or updates a source. A new source is blocked until it is allowed; <see cref="Version"/> is required to update.</summary>
public sealed record SaveNewsSourceRequest(
    string Name,
    string HomepageUrl,
    string FeedUrl,
    string FeedFormat,
    string LanguageCode,
    Guid DefaultCategoryId,
    int FetchIntervalMinutes,
    string? LicensingNote,
    bool AutoPublish,
    uint? Version);
