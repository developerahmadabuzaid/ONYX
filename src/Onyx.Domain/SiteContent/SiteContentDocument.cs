using System.Text.RegularExpressions;
using Onyx.Domain.Common;

namespace Onyx.Domain.SiteContent;

/// <summary>
/// Editor overrides for one code-owned part of the public site (the homepage, navigation and footer, website settings or a
/// site page). The code-owned copy stays the fallback: a document only ever overlays text, visibility, order, images and SEO
/// fields — never paths or structure. Editors save a draft; publishing copies it to the live version and keeps a revision.
/// </summary>
public sealed partial class SiteContentDocument : AuditableEntity
{
    public const int KeyMaxLength = 100;

    /// <summary>Upper bound for one document (both languages); the largest page override is a small fraction of this.</summary>
    public const int JsonMaxLength = 200_000;

    public const int MaxMediaReferences = 50;

    public const string HomeKey = "home";
    public const string SharedKey = "shared";
    public const string SettingsKey = "settings";
    public const string PageKeyPrefix = "page:";

    private SiteContentDocument()
    {
        Key = string.Empty;
        DraftMediaIds = [];
        PublishedMediaIds = [];
    }

    public string Key { get; private set; }

    /// <summary>The working copy (JSON object), or null when there are no unpublished changes.</summary>
    public string? DraftJson { get; private set; }

    public List<Guid> DraftMediaIds { get; private set; }

    public DateTimeOffset? DraftUpdatedAtUtc { get; private set; }

    public Guid? DraftUpdatedById { get; private set; }

    /// <summary>The live version served to the public site (JSON object), or null before the first publish.</summary>
    public string? PublishedJson { get; private set; }

    public List<Guid> PublishedMediaIds { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public Guid? PublishedById { get; private set; }

    /// <summary>Number of the latest published revision (0 before the first publish).</summary>
    public int PublishedRevision { get; private set; }

    public uint Version { get; private set; }

    public bool HasDraft => DraftJson is not null;

    public static bool IsValidKey(string? key) => key is not null && key.Length <= KeyMaxLength && KeyPattern().IsMatch(key);

    public static Result<SiteContentDocument> Create(string key)
    {
        if (!IsValidKey(key))
        {
            return Error.Validation("SiteContent.Key", "Unknown site content key.");
        }

        return new SiteContentDocument { Key = key };
    }

    public Result SaveDraft(string json, IReadOnlyCollection<Guid> mediaIds, DateTimeOffset nowUtc, Guid? userId)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(mediaIds);
        if (string.IsNullOrWhiteSpace(json) || json.Length > JsonMaxLength)
        {
            return Error.Validation("SiteContent.Size", $"Site content must be between 1 and {JsonMaxLength} characters.");
        }

        if (mediaIds.Count > MaxMediaReferences)
        {
            return Error.Validation("SiteContent.Media", $"A document may reference at most {MaxMediaReferences} media files.");
        }

        DraftJson = json;
        DraftMediaIds = mediaIds.Distinct().ToList();
        DraftUpdatedAtUtc = nowUtc;
        DraftUpdatedById = userId;
        return Result.Success();
    }

    /// <summary>Makes the draft live and returns the revision to store. Fails when there is nothing to publish.</summary>
    public Result<SiteContentRevision> Publish(DateTimeOffset nowUtc, Guid? userId)
    {
        if (DraftJson is null)
        {
            return Error.Conflict("SiteContent.NoDraft", "There are no unpublished changes to publish.");
        }

        PublishedJson = DraftJson;
        PublishedMediaIds = DraftMediaIds.ToList();
        PublishedAtUtc = nowUtc;
        PublishedById = userId;
        PublishedRevision++;
        DraftJson = null;
        DraftMediaIds = [];
        DraftUpdatedAtUtc = null;
        DraftUpdatedById = null;
        return SiteContentRevision.Create(Id, PublishedRevision, PublishedJson, PublishedMediaIds, nowUtc, userId);
    }

    public Result DiscardDraft()
    {
        if (DraftJson is null)
        {
            return Error.Conflict("SiteContent.NoDraft", "There are no unpublished changes to discard.");
        }

        DraftJson = null;
        DraftMediaIds = [];
        DraftUpdatedAtUtc = null;
        DraftUpdatedById = null;
        return Result.Success();
    }

    /// <summary>Copies an earlier published revision into the draft; it goes live only when published again.</summary>
    public Result RestoreToDraft(SiteContentRevision revision, DateTimeOffset nowUtc, Guid? userId)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (revision.DocumentId != Id)
        {
            return Error.Validation("SiteContent.Revision", "The revision belongs to another document.");
        }

        return SaveDraft(revision.Json, revision.MediaIds, nowUtc, userId);
    }

    [GeneratedRegex("^(home|shared|settings|page:[a-z0-9][a-z0-9.\\-]{0,80})$")]
    private static partial Regex KeyPattern();
}
