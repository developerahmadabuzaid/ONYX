namespace Onyx.Domain.Content;

/// <summary>
/// Workflow state of the editable working copy. Whether the public sees the item is a separate question
/// (<see cref="ContentItem.IsLive"/>): a published item that is being edited is <see cref="Draft"/> but stays live.
/// </summary>
public enum ContentStatus
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Scheduled = 3,
    Published = 4,
    Archived = 5,
}
