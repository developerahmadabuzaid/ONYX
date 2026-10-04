using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>
/// An editor-curated relationship between two content items (related content, services relevant to an industry). Rule- or
/// AI-suggested relations are proposals until an editor saves them here.
/// </summary>
public sealed class ContentRelation : Entity, IAuditable
{
    public const int NoteMaxLength = 300;

    private ContentRelation()
    {
    }

    public Guid FromContentItemId { get; private set; }

    public Guid ToContentItemId { get; private set; }

    public RelationKind Kind { get; private set; }

    public int Position { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<ContentRelation> Create(Guid fromId, Guid toId, RelationKind kind, int position, string? note, DateTimeOffset now)
    {
        if (fromId == toId)
        {
            return Error.Validation("Relation.Self", "Content cannot be related to itself.");
        }

        if (!Enum.IsDefined(kind) || note is { Length: > NoteMaxLength })
        {
            return Error.Validation("Relation.Invalid", "Unknown relation kind or note too long.");
        }

        return new ContentRelation { FromContentItemId = fromId, ToContentItemId = toId, Kind = kind, Position = position, Note = note?.Trim(), CreatedAtUtc = now };
    }
}
