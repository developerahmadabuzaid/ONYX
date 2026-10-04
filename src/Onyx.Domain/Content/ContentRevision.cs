using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>
/// An immutable snapshot of a content item's working copy (all languages, FAQs, SEO, settings). Never updated or deleted
/// except with the item itself. Restoring creates a new revision; history is never rewritten.
/// </summary>
public sealed class ContentRevision : Entity
{
    public const int HashLength = 64;
    public const int NoteMaxLength = 500;

    private ContentRevision()
    {
        Snapshot = string.Empty;
        ContentHash = string.Empty;
    }

    public Guid ContentItemId { get; private set; }

    public int Number { get; private set; }

    public RevisionKind Kind { get; private set; }

    /// <summary>JSON snapshot (jsonb). Serialized by the application layer.</summary>
    public string Snapshot { get; private set; }

    /// <summary>SHA-256 of the snapshot; consecutive identical snapshots are not stored twice.</summary>
    public string ContentHash { get; private set; }

    public string? Note { get; private set; }

    public Guid? RestoredFromRevisionId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid? CreatedById { get; private set; }

    public static ContentRevision Record(
        Guid contentItemId, int number, RevisionKind kind, string snapshot, string contentHash, string? note, Guid? restoredFrom, Guid? createdById, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot);
        if (contentHash is not { Length: HashLength } || number < 1)
        {
            throw new ArgumentException("A revision needs a positive number and a SHA-256 hash.");
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return new ContentRevision
        {
            ContentItemId = contentItemId,
            Number = number,
            Kind = kind,
            Snapshot = snapshot,
            ContentHash = contentHash,
            Note = trimmedNote is { Length: > NoteMaxLength } ? trimmedNote[..NoteMaxLength] : trimmedNote,
            RestoredFromRevisionId = restoredFrom,
            CreatedById = createdById,
            CreatedAtUtc = now,
        };
    }
}
