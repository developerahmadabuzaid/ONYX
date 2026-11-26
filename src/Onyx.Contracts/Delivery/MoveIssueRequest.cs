namespace Onyx.Contracts.Delivery;

/// <summary>Places an issue on the board (StatusId) and/or in a sprint list (ChangeSprint with SprintId, null = backlog), before BeforeIssueId (null = at the end).</summary>
public sealed record MoveIssueRequest(
    Guid? StatusId,
    bool ChangeSprint,
    Guid? SprintId,
    Guid? BeforeIssueId,
    uint Version);
