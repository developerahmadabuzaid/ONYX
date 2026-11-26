namespace Onyx.Contracts.Delivery;

/// <summary>Issue search. Mine = assigned to the caller. Sort: updated (default), created, priority, due, key.</summary>
public sealed record IssueQuery(
    Guid? ProjectId = null,
    bool Mine = false,
    Guid? AssigneeId = null,
    string? StatusCategory = null,
    string? Type = null,
    string? Priority = null,
    string? Label = null,
    Guid? SprintId = null,
    Guid? EpicId = null,
    string? Text = null,
    bool OverdueOnly = false,
    string? Sort = null,
    int Page = 1,
    int PageSize = 25,
    DateOnly? DueFrom = null,
    DateOnly? DueTo = null);
