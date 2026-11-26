namespace Onyx.Contracts.Delivery;

/// <summary>A sprint with live totals from its current issues and the snapshots recorded when it started and closed.</summary>
public sealed record SprintResponse(
    Guid Id,
    string Name,
    string? Goal,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string State,
    int IssueCount,
    int Points,
    int DoneIssueCount,
    int DonePoints,
    int? CommittedIssueCount,
    int? CommittedPoints,
    int? CompletedIssueCount,
    int? CompletedPoints,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    uint Version);
