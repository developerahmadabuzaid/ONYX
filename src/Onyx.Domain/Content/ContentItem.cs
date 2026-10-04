using System.Text.RegularExpressions;
using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>
/// A CMS content item (service pillar, service, industry, page) with one working copy per language and a separate live
/// revision. Lifecycle: Draft → PendingReview → Approved → Scheduled/Published → Archived. Approval and publishing are
/// separate steps; any edit returns the working copy to Draft so nothing unreviewed reaches the public site.
/// </summary>
public sealed partial class ContentItem : AuditableEntity, IAuditable
{
    public const int KeyMaxLength = 100;
    public const int ReviewNoteMaxLength = 1_000;
    public const int TimeZoneMaxLength = 64;

    /// <summary>Minimum number of H2 sections in every language of a published industry page (context, problems, approach).</summary>
    public const int IndustryMinimumSections = 3;

    private readonly List<ContentTranslation> _translations = [];

    private ContentItem()
    {
    }

    public ContentType Type { get; private set; }

    /// <summary>Service → its pillar (or parent service for a sub-page); page → optional parent page.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>Optional stable code-facing key (e.g. "about"), unique when set.</summary>
    public string? Key { get; private set; }

    public ContentPresentation Presentation { get; private set; }

    public ContentStatus Status { get; private set; }

    public int SortOrder { get; private set; }

    public Guid? PrimaryCtaId { get; private set; }

    public Guid? FeaturedImageId { get; private set; }

    /// <summary>The revision the public site serves. Null when not live.</summary>
    public Guid? LiveRevisionId { get; private set; }

    public Guid? SubmittedRevisionId { get; private set; }

    public Guid? SubmittedById { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public Guid? ApprovedRevisionId { get; private set; }

    public Guid? ApprovedById { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    /// <summary>Last reviewer feedback (rejection reason).</summary>
    public string? ReviewNote { get; private set; }

    public DateTimeOffset? ScheduledPublishAtUtc { get; private set; }

    public DateTimeOffset? ScheduledUnpublishAtUtc { get; private set; }

    /// <summary>IANA zone the editor scheduled in (display only; all instants are stored in UTC).</summary>
    public string? ScheduleTimeZone { get; private set; }

    public DateTimeOffset? FirstPublishedAtUtc { get; private set; }

    public DateTimeOffset? LastPublishedAtUtc { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public OfflineResolution OfflineResolution { get; private set; }

    public Guid? OfflineRedirectToId { get; private set; }

    /// <summary>Number of the latest revision; bumped by every recorded change so the row's concurrency token always moves.</summary>
    public int CurrentRevisionNumber { get; private set; }

    /// <summary>PostgreSQL <c>xmin</c> optimistic concurrency token.</summary>
    public uint Version { get; private set; }

    public IReadOnlyList<ContentTranslation> Translations => _translations;

    public bool IsLive => LiveRevisionId is not null && Status != ContentStatus.Archived;

    public bool HasBeenPublished => FirstPublishedAtUtc is not null;

    /// <summary>The item has a public page (taxonomy-only industries are tags, not pages).</summary>
    public bool HasPage => Presentation == ContentPresentation.Page;

    public static Result<ContentItem> Create(ContentType type, Guid? parentId, string? key, ContentPresentation presentation, int sortOrder)
    {
        if (!Enum.IsDefined(type) || !Enum.IsDefined(presentation))
        {
            return Error.Validation("Content.InvalidType", "Unknown content type or presentation.");
        }

        if (presentation == ContentPresentation.TaxonomyOnly && type != ContentType.Industry)
        {
            return Error.Validation("Content.InvalidPresentation", "Only industries can be taxonomy-only.");
        }

        if (ValidateParentShape(type, parentId) is { } parentError)
        {
            return parentError;
        }

        var normalizedKey = string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        if (normalizedKey is not null && !KeyPattern().IsMatch(normalizedKey))
        {
            return Error.Validation("Content.InvalidKey", "Keys use lower-case letters, digits, dots and hyphens (max 100).");
        }

        return new ContentItem { Type = type, ParentId = parentId, Key = normalizedKey, Presentation = presentation, SortOrder = sortOrder, Status = ContentStatus.Draft };
    }

    public static string BuildSlugScope(ContentType type, Guid? parentId) =>
        parentId is { } parent ? $"{type}:{parent:N}" : type.ToString();

    public ContentTranslation? GetTranslation(string languageCode) =>
        _translations.FirstOrDefault(translation => translation.LanguageCode == languageCode);

    /// <summary>Creates or updates one language of the working copy. Returns the working copy to Draft.</summary>
    public Result UpdateTranslation(
        string languageCode, string title, string? summary, string slug, IReadOnlyList<ContentBlock> body, IReadOnlyList<FaqEntry> faqs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        if (BeginEdit() is { } blocked)
        {
            return blocked;
        }

        var translation = GetTranslation(languageCode);
        var isNew = translation is null;
        translation ??= ContentTranslation.Create(Id, languageCode, BuildSlugScope(Type, ParentId));
        var updated = translation.Update(title, summary, slug, body, faqs);
        if (updated.IsFailure)
        {
            return updated;
        }

        if (isNew)
        {
            _translations.Add(translation);
        }

        return Result.Success();
    }

    /// <summary>Removes one language from the working copy (used when restoring a revision that did not have it).</summary>
    public Result RemoveTranslation(string languageCode)
    {
        if (BeginEdit() is { } blocked)
        {
            return blocked;
        }

        _translations.RemoveAll(translation => translation.LanguageCode == languageCode);
        return Result.Success();
    }

    public Result UpdateSeo(string languageCode, SeoProfile profile)
    {
        var translation = GetTranslation(languageCode);
        if (translation is null)
        {
            return Error.NotFound("Content.TranslationNotFound", "Write the content in this language before its SEO settings.");
        }

        return BeginEdit() ?? translation.UpdateSeo(profile);
    }

    public Result UpdateSettings(Guid? parentId, string? key, ContentPresentation presentation, int sortOrder, Guid? primaryCtaId, Guid? featuredImageId)
    {
        if (BeginEdit() is { } blocked)
        {
            return blocked;
        }

        var validated = Create(Type, parentId, key, presentation, sortOrder);
        if (validated.IsFailure)
        {
            return validated.Error!;
        }

        if (parentId == Id)
        {
            return Error.Validation("Content.InvalidParent", "Content cannot be its own parent.");
        }

        ParentId = parentId;
        Key = validated.Value.Key;
        Presentation = presentation;
        SortOrder = sortOrder;
        PrimaryCtaId = primaryCtaId;
        FeaturedImageId = featuredImageId;
        foreach (var translation in _translations)
        {
            translation.SetSlugScope(BuildSlugScope(Type, ParentId));
        }

        return Result.Success();
    }

    /// <summary>Records that the live page in <paramref name="languageCode"/> visibly changed (see <see cref="ContentTranslation.LiveContentModifiedAtUtc"/>).</summary>
    public void RecordLiveContentModified(string languageCode, DateTimeOffset now) => GetTranslation(languageCode)?.MarkLiveContentModified(now);

    /// <summary>Announces public URLs whose answer changed (live, moved, redirected or gone) for search-engine notification.</summary>
    public void AnnouncePublicUrls(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        foreach (var chunk in paths.Distinct(StringComparer.Ordinal).Chunk(PublicUrlsChanged.MaxPaths))
        {
            RaiseDomainEvent(new PublicUrlsChanged(Id, chunk));
        }
    }

    /// <summary>Records that a new revision of the working copy was stored (and moves the concurrency token).</summary>
    public int RecordRevision() => ++CurrentRevisionNumber;

    /// <summary>A restored revision replaces the working copy; it must be reviewed again before it can go live.</summary>
    public Result PrepareRestore() => BeginEdit() ?? Result.Success();

    public Result SubmitForReview(Guid userId, Guid revisionId, DateTimeOffset now)
    {
        if (Status != ContentStatus.Draft)
        {
            return ContentErrors.InvalidTransition(Status, "submit for review");
        }

        Status = ContentStatus.PendingReview;
        SubmittedRevisionId = revisionId;
        SubmittedById = userId;
        SubmittedAtUtc = now;
        ReviewNote = null;
        return Result.Success();
    }

    public Result Approve(Guid userId, DateTimeOffset now, IReadOnlyCollection<string> requiredLanguages)
    {
        if (Status != ContentStatus.PendingReview)
        {
            return ContentErrors.InvalidTransition(Status, "approve");
        }

        if (CheckPublishable(requiredLanguages) is { } error)
        {
            return error;
        }

        Status = ContentStatus.Approved;
        ApprovedRevisionId = SubmittedRevisionId;
        ApprovedById = userId;
        ApprovedAtUtc = now;
        return Result.Success();
    }

    public Result Reject(string? note)
    {
        if (Status != ContentStatus.PendingReview)
        {
            return ContentErrors.InvalidTransition(Status, "reject");
        }

        var trimmed = note?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > ReviewNoteMaxLength)
        {
            return Error.Validation("Content.ReviewNoteRequired", $"Explain what needs to change (1-{ReviewNoteMaxLength} characters).");
        }

        Status = ContentStatus.Draft;
        ReviewNote = trimmed;
        SubmittedRevisionId = null;
        return Result.Success();
    }

    public Result Publish(DateTimeOffset now, IReadOnlyCollection<string> requiredLanguages)
    {
        if (Status != ContentStatus.Approved)
        {
            return ContentErrors.InvalidTransition(Status, "publish");
        }

        return GoLive(now, requiredLanguages);
    }

    public Result Schedule(DateTimeOffset publishAtUtc, string? timeZone, DateTimeOffset now)
    {
        if (Status != ContentStatus.Approved)
        {
            return ContentErrors.InvalidTransition(Status, "schedule");
        }

        if (publishAtUtc <= now)
        {
            return ContentErrors.ScheduleInPast;
        }

        if (timeZone is { Length: > TimeZoneMaxLength })
        {
            return Error.Validation("Content.InvalidTimeZone", "Unknown time zone.");
        }

        Status = ContentStatus.Scheduled;
        ScheduledPublishAtUtc = publishAtUtc.ToUniversalTime();
        ScheduleTimeZone = timeZone;
        return Result.Success();
    }

    public Result CancelSchedule()
    {
        if (Status != ContentStatus.Scheduled)
        {
            return ContentErrors.InvalidTransition(Status, "cancel the schedule of");
        }

        Status = ContentStatus.Approved;
        ScheduledPublishAtUtc = null;
        return Result.Success();
    }

    /// <summary>Called by the Worker. Publishes only an approved, scheduled working copy whose time has come.</summary>
    public Result PublishScheduled(DateTimeOffset now, IReadOnlyCollection<string> requiredLanguages)
    {
        if (Status != ContentStatus.Scheduled || ScheduledPublishAtUtc is null || ScheduledPublishAtUtc > now)
        {
            return ContentErrors.InvalidTransition(Status, "publish on schedule");
        }

        return GoLive(now, requiredLanguages);
    }

    /// <summary>Returns a scheduled item to Approved when its scheduled publication cannot happen (e.g. the parent went offline).</summary>
    public void FailSchedule()
    {
        if (Status == ContentStatus.Scheduled)
        {
            Status = ContentStatus.Approved;
            ScheduledPublishAtUtc = null;
        }
    }

    public Result ScheduleUnpublish(DateTimeOffset unpublishAtUtc, DateTimeOffset now)
    {
        if (!IsLive)
        {
            return ContentErrors.NotLive;
        }

        if (unpublishAtUtc <= now)
        {
            return ContentErrors.ScheduleInPast;
        }

        ScheduledUnpublishAtUtc = unpublishAtUtc.ToUniversalTime();
        return Result.Success();
    }

    public Result CancelUnpublishSchedule()
    {
        if (ScheduledUnpublishAtUtc is null)
        {
            return Error.Conflict("Content.NoUnpublishSchedule", "No unpublish is scheduled.");
        }

        ScheduledUnpublishAtUtc = null;
        return Result.Success();
    }

    /// <summary>
    /// Takes live content offline. Its URLs follow <paramref name="resolution"/>; without a decision they answer 410 and are
    /// flagged, never silently redirected to the homepage.
    /// </summary>
    public Result Unpublish(OfflineResolution resolution, Guid? redirectToId)
    {
        if (!IsLive)
        {
            return ContentErrors.NotLive;
        }

        if (ValidateResolution(resolution, redirectToId) is { } error)
        {
            return error;
        }

        LiveRevisionId = null;
        ScheduledUnpublishAtUtc = null;
        if (Status == ContentStatus.Published)
        {
            Status = ContentStatus.Draft;
        }

        SetResolution(resolution, redirectToId);
        RaiseDomainEvent(new ContentPublicationChanged(Id, false));
        return Result.Success();
    }

    /// <summary>Called by the Worker when a scheduled unpublish is due. URLs answer 410 and are flagged for a decision.</summary>
    public Result UnpublishScheduled(DateTimeOffset now)
    {
        if (ScheduledUnpublishAtUtc is null || ScheduledUnpublishAtUtc > now)
        {
            return Error.Conflict("Content.NotDue", "The unpublish is not due.");
        }

        return Unpublish(OfflineResolution.PendingDecision, null);
    }

    public Result Archive(OfflineResolution resolution, Guid? redirectToId, DateTimeOffset now)
    {
        if (Status == ContentStatus.Archived)
        {
            return ContentErrors.InvalidTransition(Status, "archive");
        }

        if (IsLive)
        {
            var offline = Unpublish(resolution, redirectToId);
            if (offline.IsFailure)
            {
                return offline;
            }
        }
        else if (HasBeenPublished && resolution != OfflineResolution.None)
        {
            if (ValidateResolution(resolution, redirectToId) is { } error)
            {
                return error;
            }

            SetResolution(resolution, redirectToId);
        }

        Status = ContentStatus.Archived;
        ArchivedAtUtc = now;
        ClearWorkflow();
        return Result.Success();
    }

    public Result RestoreFromArchive()
    {
        if (Status != ContentStatus.Archived)
        {
            return ContentErrors.InvalidTransition(Status, "restore");
        }

        Status = ContentStatus.Draft;
        ArchivedAtUtc = null;
        return Result.Success();
    }

    /// <summary>Records the editor's decision for the URLs of offline content that was once public.</summary>
    public Result ResolveOffline(OfflineResolution resolution, Guid? redirectToId)
    {
        if (IsLive || !HasBeenPublished)
        {
            return Error.Conflict("Content.NothingToResolve", "Only content that was public and is now offline needs a URL decision.");
        }

        if (resolution is not (OfflineResolution.Gone or OfflineResolution.RedirectToContent) || ValidateResolution(resolution, redirectToId) is not null)
        {
            return ContentErrors.InvalidResolution;
        }

        SetResolution(resolution, redirectToId);
        return Result.Success();
    }

    /// <summary>Every configured language complete; industry pages meet the content minimum.</summary>
    public Error? CheckPublishable(IReadOnlyCollection<string> requiredLanguages)
    {
        ArgumentNullException.ThrowIfNull(requiredLanguages);
        var translations = requiredLanguages.Select(GetTranslation).ToList();
        if (translations.Any(translation => translation is null || !translation.IsComplete))
        {
            return ContentErrors.IncompleteTranslations;
        }

        if (Type == ContentType.Industry && HasPage && !translations.All(translation => MeetsIndustryMinimum(translation!)))
        {
            return ContentErrors.IndustryMinimum;
        }

        return null;
    }

    public static bool MeetsIndustryMinimum(ContentTranslation translation)
    {
        ArgumentNullException.ThrowIfNull(translation);
        if (string.IsNullOrWhiteSpace(translation.Summary))
        {
            return false;
        }

        // A section is an H2 followed by at least one non-heading block.
        var sections = 0;
        for (var index = 0; index < translation.Body.Count - 1; index++)
        {
            if (translation.Body[index] is { Type: ContentBlockType.Heading, Level: 2 } && translation.Body[index + 1].Type != ContentBlockType.Heading)
            {
                sections++;
            }
        }

        return sections >= IndustryMinimumSections;
    }

    private static Error? ValidateParentShape(ContentType type, Guid? parentId) => type switch
    {
        ContentType.ServicePillar or ContentType.Industry when parentId is not null =>
            Error.Validation("Content.InvalidParent", $"A {type} cannot have a parent."),
        ContentType.Service when parentId is null =>
            Error.Validation("Content.InvalidParent", "A service belongs to a service pillar (or to a parent service for a sub-page)."),
        _ => null,
    };

    private Result GoLive(DateTimeOffset now, IReadOnlyCollection<string> requiredLanguages)
    {
        if (CheckPublishable(requiredLanguages) is { } error)
        {
            return error;
        }

        if (!HasPage)
        {
            return Error.Validation("Content.NoPage", "Taxonomy-only industries have no public page to publish.");
        }

        LiveRevisionId = ApprovedRevisionId ?? throw new InvalidOperationException("Approved content always has an approved revision.");
        Status = ContentStatus.Published;
        FirstPublishedAtUtc ??= now;
        LastPublishedAtUtc = now;
        ScheduledPublishAtUtc = null;
        SetResolution(OfflineResolution.None, null);
        RaiseDomainEvent(new ContentPublicationChanged(Id, true));
        return Result.Success();
    }

    private Error? BeginEdit()
    {
        if (Status == ContentStatus.Archived)
        {
            return ContentErrors.Archived;
        }

        if (Status is ContentStatus.PendingReview or ContentStatus.Approved or ContentStatus.Scheduled or ContentStatus.Published)
        {
            Status = ContentStatus.Draft;
            ClearWorkflow();
        }

        return null;
    }

    private void ClearWorkflow()
    {
        SubmittedRevisionId = null;
        ApprovedRevisionId = null;
        ApprovedById = null;
        ApprovedAtUtc = null;
        ScheduledPublishAtUtc = null;
    }

    private Error? ValidateResolution(OfflineResolution resolution, Guid? redirectToId) => resolution switch
    {
        OfflineResolution.RedirectToContent when redirectToId is null || redirectToId == Id => ContentErrors.InvalidResolution,
        OfflineResolution.RedirectToContent => null,
        OfflineResolution.None or OfflineResolution.PendingDecision or OfflineResolution.Gone when redirectToId is not null => ContentErrors.InvalidResolution,
        OfflineResolution.None or OfflineResolution.PendingDecision or OfflineResolution.Gone => null,
        _ => ContentErrors.InvalidResolution,
    };

    private void SetResolution(OfflineResolution resolution, Guid? redirectToId)
    {
        OfflineResolution = resolution == OfflineResolution.None && HasBeenPublished && !IsLive ? OfflineResolution.PendingDecision : resolution;
        OfflineRedirectToId = resolution == OfflineResolution.RedirectToContent ? redirectToId : null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
