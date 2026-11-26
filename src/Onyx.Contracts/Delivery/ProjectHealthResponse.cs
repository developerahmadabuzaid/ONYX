namespace Onyx.Contracts.Delivery;

/// <summary>
/// A project with its progress and a stated health rule: Completed and OnHold come from the project status; otherwise Delayed
/// (past its target date), AtRisk (has overdue open issues) or OnTrack.
/// </summary>
public sealed record ProjectHealthResponse(
    Guid Id,
    string Key,
    string Name,
    string Status,
    string Health,
    string Priority,
    int TotalIssues,
    int DoneIssues,
    int OverdueIssues,
    int MemberCount,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    DateTimeOffset UpdatedAtUtc);
