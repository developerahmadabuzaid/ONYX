namespace Onyx.Domain.News;

/// <summary>
/// Stored as text (architecture.md §17.2). Only <see cref="Published"/> items are public. A deleted item keeps its source URL
/// as a tombstone so the same article is never ingested again.
/// </summary>
public enum NewsItemStatus
{
    Held = 0,
    Published = 1,
    Hidden = 2,
    Unpublished = 3,
    Deleted = 4,
}
