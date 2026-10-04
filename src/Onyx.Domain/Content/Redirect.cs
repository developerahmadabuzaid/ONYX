using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>
/// An editor-managed rule for a path that is not owned by live content (e.g. a legacy URL or a deleted page): a permanent
/// redirect to an internal target (a content item, followed to its current URL, or a site path in the same language), or an
/// explicit 410 Gone. Never an external URL, never an automatic homepage redirect.
/// </summary>
public sealed class Redirect : AuditableEntity, IAuditable
{
    public const int NoteMaxLength = 500;

    private Redirect()
    {
        LanguageCode = string.Empty;
        FromPath = string.Empty;
    }

    public string LanguageCode { get; private set; }

    /// <summary>Path without the language prefix, NFC, no leading or trailing slash.</summary>
    public string FromPath { get; private set; }

    public RedirectKind Kind { get; private set; }

    public Guid? TargetContentItemId { get; private set; }

    /// <summary>Path without the language prefix (same language as <see cref="LanguageCode"/>).</summary>
    public string? TargetPath { get; private set; }

    public string? Note { get; private set; }

    public bool IsActive { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Publishing;

    public static Result<Redirect> Create(string languageCode, string fromPath, RedirectKind kind, Guid? targetContentItemId, string? targetPath, string? note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        if (!IsSafePath(fromPath))
        {
            return Error.Validation("Redirect.InvalidPath", "A source path is required.");
        }

        var redirect = new Redirect { LanguageCode = languageCode, FromPath = fromPath, IsActive = true };
        var updated = redirect.Update(kind, targetContentItemId, targetPath, note, isActive: true);
        return updated.IsSuccess ? redirect : updated.Error!;
    }

    public Result Update(RedirectKind kind, Guid? targetContentItemId, string? targetPath, string? note, bool isActive)
    {
        if (!Enum.IsDefined(kind))
        {
            return Error.Validation("Redirect.InvalidKind", "Unknown redirect kind.");
        }

        var hasPath = !string.IsNullOrWhiteSpace(targetPath);
        if (kind == RedirectKind.Gone ? targetContentItemId is not null || hasPath : (targetContentItemId is null) == !hasPath)
        {
            return Error.Validation("Redirect.InvalidTarget", "A redirect needs exactly one target (content item or site path); Gone has none.");
        }

        if (hasPath && !IsSafePath(targetPath))
        {
            return Error.Validation("Redirect.InvalidTarget", "Redirect targets are site paths in the same language (no external URLs).");
        }

        if (hasPath && string.Equals(targetPath, FromPath, StringComparison.Ordinal))
        {
            return Error.Validation("Redirect.Loop", "A redirect cannot point to itself.");
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > NoteMaxLength })
        {
            return Error.Validation("Redirect.InvalidNote", $"Notes are limited to {NoteMaxLength} characters.");
        }

        Kind = kind;
        TargetContentItemId = targetContentItemId;
        TargetPath = hasPath ? targetPath : null;
        Note = trimmedNote;
        IsActive = isActive;
        RaiseDomainEvent(new PublicUrlsChanged(Id, [PublicPath]));
        return Result.Success();
    }

    /// <summary>The rule's source URL as a site path with its language prefix.</summary>
    public string PublicPath => $"/{LanguageCode}/{FromPath}";

    /// <summary>Called before the rule is deleted: its URL now answers differently (usually 404).</summary>
    public void AnnounceRemoval() => RaiseDomainEvent(new PublicUrlsChanged(Id, [PublicPath]));

    /// <summary>
    /// A stored path (no language prefix, no leading slash): no scheme or host (no colon), no query or fragment, no backslashes or
    /// control characters, and no dot segments, so a rule can never leave the site or climb out of its language.
    /// </summary>
    public static bool IsSafePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.Length <= ContentPath.PathMaxLength && path[0] != '/'
        && path.IndexOfAny([':', '\\', '?', '#', ' ']) < 0 && !ContentBodyRules.HasControlCharacters(path, allowNewLines: false)
        && !path.Split('/').Any(segment => segment is "" or "." or "..");
}
