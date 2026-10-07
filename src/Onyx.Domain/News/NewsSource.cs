using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.News;

/// <summary>
/// A news feed (architecture.md §17.2). A new source starts <see cref="NewsSourceStatus.Blocked"/>; it is fetched only after an
/// administrator records the reviewed licensing/terms note and allowlists it. Feed and homepage URLs must be public https URLs.
/// Fetch failures back off exponentially and the first failure of a run of failures raises one notification.
/// </summary>
public sealed class NewsSource : AuditableEntity, IAuditable
{
    public const int NameMaxLength = 150;
    public const int LicensingNoteMaxLength = 2_000;
    public const int LastErrorMaxLength = 300;
    public const int LanguageCodeMaxLength = 10;
    public const int MinFetchIntervalMinutes = 15;
    public const int MaxFetchIntervalMinutes = 1_440;
    public const int DefaultFetchIntervalMinutes = 60;

    /// <summary>Longest wait after repeated failures.</summary>
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(24);

    private NewsSource()
    {
        Name = string.Empty;
        HomepageUrl = string.Empty;
        FeedUrl = string.Empty;
        LanguageCode = string.Empty;
    }

    public string Name { get; private set; }

    public string HomepageUrl { get; private set; }

    /// <summary>May carry a publisher-issued key in its query, so audit entries record only that it changed.</summary>
    [SensitiveData]
    public string FeedUrl { get; private set; }

    public NewsFeedFormat FeedFormat { get; private set; }

    /// <summary>Language the source publishes in (its items' source language).</summary>
    public string LanguageCode { get; private set; }

    public Guid DefaultCategoryId { get; private set; }

    public NewsSourceStatus Status { get; private set; }

    /// <summary>Reviewed licensing/terms note; required before the source can be allowlisted.</summary>
    public string? LicensingNote { get; private set; }

    /// <summary>Items with a valid Onyx summary from the external service are published without review.</summary>
    public bool AutoPublish { get; private set; }

    public int FetchIntervalMinutes { get; private set; }

    [NotAudited]
    public DateTimeOffset? NextFetchAtUtc { get; private set; }

    [NotAudited]
    public DateTimeOffset? LastFetchedAtUtc { get; private set; }

    [NotAudited]
    public DateTimeOffset? LastSucceededAtUtc { get; private set; }

    /// <summary>Safe failure code and short description; never response bodies, credentials or query strings.</summary>
    [NotAudited]
    public string? LastError { get; private set; }

    [NotAudited]
    public int ConsecutiveFailures { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Content;

    public bool IsAllowed => Status == NewsSourceStatus.Allowed;

    public static Result<NewsSource> Create(
        string? name, string? homepageUrl, string? feedUrl, NewsFeedFormat feedFormat, string? languageCode, Guid defaultCategoryId,
        int fetchIntervalMinutes, string? licensingNote, bool autoPublish)
    {
        var source = new NewsSource { Status = NewsSourceStatus.Blocked };
        var applied = source.Apply(name, homepageUrl, feedUrl, feedFormat, languageCode, defaultCategoryId, fetchIntervalMinutes, licensingNote, autoPublish);
        return applied.IsSuccess ? source : applied.Error!;
    }

    public Result Update(
        string? name, string? homepageUrl, string? feedUrl, NewsFeedFormat feedFormat, string? languageCode, Guid defaultCategoryId,
        int fetchIntervalMinutes, string? licensingNote, bool autoPublish)
    {
        var previousFeed = FeedUrl;
        var applied = Apply(name, homepageUrl, feedUrl, feedFormat, languageCode, defaultCategoryId, fetchIntervalMinutes, licensingNote, autoPublish);
        if (applied.IsFailure)
        {
            return applied;
        }

        if (IsAllowed && LicensingNote is null)
        {
            // The terms note is what justified the allowlisting; removing it blocks the source again.
            Status = NewsSourceStatus.Blocked;
        }

        if (!string.Equals(previousFeed, FeedUrl, StringComparison.Ordinal))
        {
            ConsecutiveFailures = 0;
            LastError = null;
            NextFetchAtUtc = null;
        }

        return Result.Success();
    }

    /// <summary>Allowlists the source after its licensing/terms were reviewed; it is fetched on the next ingestion run.</summary>
    public Result Allow(DateTimeOffset now)
    {
        if (LicensingNote is null)
        {
            return Error.Validation("News.LicensingNoteRequired", "Record the reviewed licensing/terms note before allowing the source.");
        }

        if (!IsAllowed)
        {
            Status = NewsSourceStatus.Allowed;
            ConsecutiveFailures = 0;
            LastError = null;
            NextFetchAtUtc = now;
        }

        return Result.Success();
    }

    public void Block()
    {
        Status = NewsSourceStatus.Blocked;
        NextFetchAtUtc = null;
    }

    public bool IsDue(DateTimeOffset now) => IsAllowed && (NextFetchAtUtc is null || NextFetchAtUtc <= now);

    public void RecordSuccess(DateTimeOffset now)
    {
        LastFetchedAtUtc = now;
        LastSucceededAtUtc = now;
        LastError = null;
        ConsecutiveFailures = 0;
        NextFetchAtUtc = now.AddMinutes(FetchIntervalMinutes);
    }

    /// <summary>Records a failed fetch and backs off (interval × 2^(failures−1), at most a day). The first failure notifies.</summary>
    public void RecordFailure(DateTimeOffset now, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        LastFetchedAtUtc = now;
        LastError = error.Length > LastErrorMaxLength ? error[..LastErrorMaxLength] : error;
        ConsecutiveFailures++;
        var factor = Math.Pow(2, Math.Min(ConsecutiveFailures - 1, 10));
        var wait = TimeSpan.FromMinutes(Math.Min(FetchIntervalMinutes * factor, MaxBackoff.TotalMinutes));
        NextFetchAtUtc = now.Add(wait);
        if (ConsecutiveFailures == 1)
        {
            RaiseDomainEvent(new NewsSourceFailing(Id, Name));
        }
    }

    private Result Apply(
        string? name, string? homepageUrl, string? feedUrl, NewsFeedFormat feedFormat, string? languageCode, Guid defaultCategoryId,
        int fetchIntervalMinutes, string? licensingNote, bool autoPublish)
    {
        var cleanName = name?.Trim();
        if (string.IsNullOrEmpty(cleanName) || cleanName.Length > NameMaxLength)
        {
            return Error.Validation("News.InvalidSourceName", $"Enter the publisher name (at most {NameMaxLength} characters).");
        }

        var homepage = NewsUrl.Parse(homepageUrl, requireHttps: true);
        if (homepage.IsFailure)
        {
            return Error.Validation("News.InvalidHomepageUrl", $"Homepage: {homepage.Error!.Message}");
        }

        var feed = NewsUrl.Parse(feedUrl, requireHttps: true);
        if (feed.IsFailure)
        {
            return Error.Validation("News.InvalidFeedUrl", $"Feed: {feed.Error!.Message}");
        }

        if (!Enum.IsDefined(feedFormat))
        {
            return Error.Validation("News.InvalidFeedFormat", "The feed format is Rss or Atom.");
        }

        var language = languageCode?.Trim();
        if (string.IsNullOrEmpty(language) || language.Length > LanguageCodeMaxLength)
        {
            return Error.Validation("News.InvalidLanguage", "Choose the source language.");
        }

        if (defaultCategoryId == Guid.Empty)
        {
            return Error.Validation("News.CategoryRequired", "Choose the default category.");
        }

        if (fetchIntervalMinutes is < MinFetchIntervalMinutes or > MaxFetchIntervalMinutes)
        {
            return Error.Validation("News.InvalidInterval", $"The fetch interval is {MinFetchIntervalMinutes}–{MaxFetchIntervalMinutes} minutes.");
        }

        var note = string.IsNullOrWhiteSpace(licensingNote) ? null : licensingNote.Trim();
        if (note is { Length: > LicensingNoteMaxLength })
        {
            return Error.Validation("News.LicensingNoteTooLong", $"The licensing note is limited to {LicensingNoteMaxLength} characters.");
        }

        Name = cleanName;
        HomepageUrl = homepage.Value.AbsoluteUri;
        FeedUrl = feed.Value.AbsoluteUri;
        FeedFormat = feedFormat;
        LanguageCode = language;
        DefaultCategoryId = defaultCategoryId;
        FetchIntervalMinutes = fetchIntervalMinutes;
        LicensingNote = note;
        AutoPublish = autoPublish;
        return Result.Success();
    }
}
