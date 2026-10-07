using System.Text;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.News;

/// <summary>
/// One external article reported by an allowlisted source (architecture.md §17). Only metadata is stored (canonical URL,
/// original title, publisher, the source's own publication time) plus Onyx's own headline and summary per language; the
/// article text is never stored. Rules enforced here:
/// <list type="bullet">
/// <item>nothing is public without an Onyx headline and summary in every site language;</item>
/// <item>automatic publication needs summaries produced by the external service (never edited placeholders), an allowlisted
/// source set to auto-publish, and no similar recent item;</item>
/// <item>a deleted item keeps its URL as a tombstone (never re-ingested) but loses its summaries.</item>
/// </list>
/// </summary>
public sealed class NewsItem : AuditableEntity, IAuditable
{
    public const int OriginalTitleMaxLength = 500;
    public const int ClusterKeyMaxLength = 200;
    public const int ProviderMaxLength = 100;

    /// <summary>A source date this far in the future is not trusted (and not shown).</summary>
    public static readonly TimeSpan MaxFutureSkew = TimeSpan.FromDays(1);

    private readonly List<NewsItemTranslation> _translations = [];

    private NewsItem()
    {
        OriginalUrl = string.Empty;
        OriginalUrlHash = string.Empty;
        OriginalTitle = string.Empty;
        Publisher = string.Empty;
        SourceLanguageCode = string.Empty;
        ClusterKey = string.Empty;
    }

    public Guid SourceId { get; private set; }

    /// <summary>Canonical link to the original article (<see cref="NewsUrl.Canonicalize"/>).</summary>
    public string OriginalUrl { get; private set; }

    public string OriginalUrlHash { get; private set; }

    /// <summary>The source's title, kept for editors and deduplication; public pages show Onyx's own headline.</summary>
    public string OriginalTitle { get; private set; }

    /// <summary>The publisher name as configured for the source when the item was ingested (never taken from feed text).</summary>
    public string Publisher { get; private set; }

    public string SourceLanguageCode { get; private set; }

    /// <summary>When the source says it published the article; null when the feed gives no usable date (never invented).</summary>
    public DateTimeOffset? SourcePublishedAtUtc { get; private set; }

    public DateTimeOffset IngestedAtUtc { get; private set; }

    public Guid CategoryId { get; private set; }

    /// <summary>Normalized original title; items sharing it within a short window are treated as the same story.</summary>
    public string ClusterKey { get; private set; }

    public Guid? SimilarToItemId { get; private set; }

    public NewsItemStatus Status { get; private set; }

    public NewsHoldReason? HoldReason { get; private set; }

    public NewsSummaryOrigin? SummaryOrigin { get; private set; }

    /// <summary>Provider key of the service that generated the summaries (no model output or prompt is stored).</summary>
    public string? SummaryProvider { get; private set; }

    public bool AutoPublished { get; private set; }

    /// <summary>Search engines may index the item page (default false: <c>noindex, follow</c>, architecture.md §17.3).</summary>
    public bool Indexable { get; private set; }

    /// <summary>When Onyx (last) published the item; distinct from <see cref="SourcePublishedAtUtc"/>.</summary>
    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public DateTimeOffset? FirstPublishedAtUtc { get; private set; }

    public IReadOnlyCollection<NewsItemTranslation> Translations => _translations.AsReadOnly();

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Content;

    public bool IsPublic => Status == NewsItemStatus.Published;

    public static Result<NewsItem> Ingest(
        NewsSource source, string canonicalUrl, string? originalTitle, DateTimeOffset? sourcePublishedAtUtc, Guid categoryId,
        string clusterKey, Guid? similarToItemId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalUrl);
        var title = CleanText(originalTitle);
        if (title.Length == 0)
        {
            return Error.Validation("News.MissingTitle", "The feed entry has no title.");
        }

        if (canonicalUrl.Length > NewsUrl.MaxLength || categoryId == Guid.Empty)
        {
            return Error.Validation("News.InvalidEntry", "The feed entry cannot be stored.");
        }

        var key = clusterKey.Length > ClusterKeyMaxLength ? clusterKey[..ClusterKeyMaxLength] : clusterKey;
        return new NewsItem
        {
            SourceId = source.Id,
            OriginalUrl = canonicalUrl,
            OriginalUrlHash = NewsUrl.Hash(canonicalUrl),
            OriginalTitle = title.Length > OriginalTitleMaxLength ? title[..OriginalTitleMaxLength] : title,
            Publisher = source.Name,
            SourceLanguageCode = source.LanguageCode,
            SourcePublishedAtUtc = sourcePublishedAtUtc is { } published && published <= now.Add(MaxFutureSkew) ? published.ToUniversalTime() : null,
            IngestedAtUtc = now,
            CategoryId = categoryId,
            ClusterKey = key,
            SimilarToItemId = similarToItemId,
            Status = NewsItemStatus.Held,
            HoldReason = similarToItemId is null ? NewsHoldReason.SummaryUnavailable : NewsHoldReason.SimilarItem,
        };
    }

    public NewsItemTranslation? Translation(string languageCode) => _translations.FirstOrDefault(translation => translation.LanguageCode == languageCode);

    public bool HasCompleteSummaries(IReadOnlyCollection<string> languageCodes)
    {
        ArgumentNullException.ThrowIfNull(languageCodes);
        return languageCodes.Count > 0 && languageCodes.All(language => Translation(language) is { } translation && IsValidText(translation.Title, translation.Summary));
    }

    /// <summary>
    /// Stores summaries from the external service. Each text must pass the same checks as an editor's text; the caller supplies a
    /// unique slug for every language that has no translation yet.
    /// </summary>
    public Result ApplyGeneratedSummaries(IReadOnlyList<NewsText> texts, string providerKey, IReadOnlyDictionary<string, string> newSlugs)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(newSlugs);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        if (Status != NewsItemStatus.Held || SummaryOrigin == NewsSummaryOrigin.Manual)
        {
            // Never overwrite an editor's work or a public item.
            return Error.Conflict("News.NotGeneratable", "Only held items without editor summaries take generated summaries.");
        }

        foreach (var text in texts)
        {
            var valid = Validate(text.Title, text.Summary);
            if (valid.IsFailure)
            {
                return valid.Error!;
            }
        }

        foreach (var text in texts)
        {
            var applied = Upsert(text.LanguageCode, CleanText(text.Title), CleanText(text.Summary), newSlugs.GetValueOrDefault(text.LanguageCode));
            if (applied.IsFailure)
            {
                return applied;
            }
        }

        SummaryOrigin = NewsSummaryOrigin.Generated;
        SummaryProvider = providerKey.Length > ProviderMaxLength ? providerKey[..ProviderMaxLength] : providerKey;
        return Result.Success();
    }

    /// <summary>An editor writes or edits the headline and summary in one language.</summary>
    public Result EditTranslation(string languageCode, string? title, string? summary, string? newSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        if (Status == NewsItemStatus.Deleted)
        {
            return Error.Conflict("News.Deleted", "This item was deleted.");
        }

        var valid = Validate(title, summary);
        if (valid.IsFailure)
        {
            return valid;
        }

        var applied = Upsert(languageCode, CleanText(title), CleanText(summary), newSlug);
        if (applied.IsFailure)
        {
            return applied;
        }

        SummaryOrigin = NewsSummaryOrigin.Manual;
        SummaryProvider = null;
        if (IsPublic)
        {
            RaiseDomainEvent(new NewsItemPublicationChanged(Id));
        }

        return Result.Success();
    }

    /// <summary>An editor publishes the item. Requires an Onyx headline and summary in every site language.</summary>
    public Result Publish(DateTimeOffset now, IReadOnlyCollection<string> languageCodes)
    {
        if (Status is not (NewsItemStatus.Held or NewsItemStatus.Hidden or NewsItemStatus.Unpublished))
        {
            return Error.Conflict("News.NotPublishable", $"A {Status} item cannot be published.");
        }

        if (!HasCompleteSummaries(languageCodes))
        {
            return Error.Validation("News.SummaryRequired", "Write the Onyx headline and summary in every language before publishing.");
        }

        GoLive(now, automatic: false);
        return Result.Success();
    }

    /// <summary>
    /// Publishes without review: only a held item whose summaries were generated by the external service and not edited, from
    /// the allowlisted source that ingested it and is set to auto-publish, with no similar recent item.
    /// </summary>
    public Result AutoPublish(DateTimeOffset now, NewsSource source, IReadOnlyCollection<string> languageCodes)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (Status != NewsItemStatus.Held || SimilarToItemId is not null)
        {
            return Error.Conflict("News.NotAutoPublishable", "Only held items without a similar recent item can be published automatically.");
        }

        if (SummaryOrigin != NewsSummaryOrigin.Generated || !HasCompleteSummaries(languageCodes))
        {
            return Error.Conflict("News.SummaryRequired", "Automatic publication needs generated summaries in every language.");
        }

        if (source.Id != SourceId || !source.IsAllowed || !source.AutoPublish)
        {
            return Error.Conflict("News.SourceNotAutoPublishing", "The source is not allowlisted for automatic publication.");
        }

        GoLive(now, automatic: true);
        return Result.Success();
    }

    /// <summary>Records why a held item waits for an editor.</summary>
    public void KeepHeld(NewsHoldReason reason)
    {
        if (Status == NewsItemStatus.Held)
        {
            HoldReason = reason;
        }
    }

    public Result Hide() => TakeDown(NewsItemStatus.Hidden, NewsItemStatus.Held, NewsItemStatus.Published, NewsItemStatus.Unpublished);

    public Result Unpublish() => TakeDown(NewsItemStatus.Unpublished, NewsItemStatus.Published);

    /// <summary>Removes the item from the hub for good; the URL stays as a tombstone so the article is never ingested again.</summary>
    public Result Delete()
    {
        if (Status == NewsItemStatus.Deleted)
        {
            return Result.Success();
        }

        var wasPublic = IsPublic;
        Status = NewsItemStatus.Deleted;
        HoldReason = null;
        Indexable = false;
        _translations.Clear();
        if (wasPublic)
        {
            RaiseDomainEvent(new NewsItemPublicationChanged(Id));
        }

        return Result.Success();
    }

    public Result SetIndexable(bool indexable)
    {
        if (Status == NewsItemStatus.Deleted)
        {
            return Error.Conflict("News.Deleted", "This item was deleted.");
        }

        if (Indexable != indexable)
        {
            Indexable = indexable;
            if (IsPublic)
            {
                RaiseDomainEvent(new NewsItemPublicationChanged(Id));
            }
        }

        return Result.Success();
    }

    public void ChangeCategory(Guid categoryId)
    {
        if (categoryId != Guid.Empty && categoryId != CategoryId)
        {
            CategoryId = categoryId;
            if (IsPublic)
            {
                RaiseDomainEvent(new NewsItemPublicationChanged(Id));
            }
        }
    }

    /// <summary>Plain text: control characters removed and whitespace collapsed.</summary>
    public static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value.Normalize(NormalizationForm.FormC))
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    public static Result Validate(string? title, string? summary)
    {
        var cleanTitle = CleanText(title);
        var cleanSummary = CleanText(summary);
        if (cleanTitle.Length is 0 or > NewsItemTranslation.TitleMaxLength)
        {
            return Error.Validation("News.InvalidTitle", $"The headline is 1–{NewsItemTranslation.TitleMaxLength} characters.");
        }

        if (cleanSummary.Length is < NewsItemTranslation.SummaryMinLength or > NewsItemTranslation.SummaryMaxLength)
        {
            return Error.Validation(
                "News.InvalidSummary", $"The summary is {NewsItemTranslation.SummaryMinLength}–{NewsItemTranslation.SummaryMaxLength} characters.");
        }

        if (ContainsMarkup(cleanTitle) || ContainsMarkup(cleanSummary))
        {
            return Error.Validation("News.MarkupNotAllowed", "Headlines and summaries are plain text.");
        }

        return Result.Success();
    }

    private static bool IsValidText(string title, string summary) => Validate(title, summary).IsSuccess;

    private static bool ContainsMarkup(string text) => text.Contains('<', StringComparison.Ordinal) || text.Contains('>', StringComparison.Ordinal);

    private Result Upsert(string languageCode, string title, string summary, string? newSlug)
    {
        if (Translation(languageCode) is { } existing)
        {
            // The slug is part of a public URL: it may change only before the item was ever public.
            existing.Change(title, summary, FirstPublishedAtUtc is null && newSlug is not null ? newSlug : null);
            return Result.Success();
        }

        if (newSlug is null || !Content.Slug.IsValid(newSlug))
        {
            return Error.Validation("News.InvalidSlug", "A valid slug is required for a new language.");
        }

        _translations.Add(new NewsItemTranslation(Id, languageCode, title, summary, newSlug));
        return Result.Success();
    }

    private void GoLive(DateTimeOffset now, bool automatic)
    {
        Status = NewsItemStatus.Published;
        HoldReason = null;
        AutoPublished = automatic;
        PublishedAtUtc = now;
        FirstPublishedAtUtc ??= now;
        RaiseDomainEvent(new NewsItemPublicationChanged(Id));
    }

    private Result TakeDown(NewsItemStatus target, params NewsItemStatus[] from)
    {
        if (Status == target)
        {
            return Result.Success();
        }

        if (!from.Contains(Status))
        {
            return Error.Conflict("News.InvalidTransition", $"A {Status} item cannot become {target}.");
        }

        var wasPublic = IsPublic;
        Status = target;
        HoldReason = null;
        if (wasPublic)
        {
            RaiseDomainEvent(new NewsItemPublicationChanged(Id));
        }

        return Result.Success();
    }
}
