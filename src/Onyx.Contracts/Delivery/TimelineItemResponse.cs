namespace Onyx.Contracts.Delivery;

/// <summary>An issue or epic on a timeline. Epic progress counts its child issues.</summary>
public sealed record TimelineItemResponse(
    Guid Id,
    string Key,
    string Title,
    string Type,
    string StatusName,
    string StatusCategory,
    string Priority,
    string? AssigneeName,
    DateOnly? StartDate,
    DateOnly? DueDate,
    Guid? EpicId,
    int ChildCount,
    int DoneChildCount,
    bool IsOverdue,
    uint Version);
