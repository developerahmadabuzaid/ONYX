namespace Onyx.Contracts.Delivery;

/// <summary>Open sprints with their issues, then the unscheduled backlog, plus the project's epics for grouping.</summary>
public sealed record BacklogResponse(
    Guid ProjectId,
    string ProjectKey,
    string ProjectName,
    IReadOnlyList<SprintBacklogResponse> Sprints,
    IReadOnlyList<IssueListItemResponse> Backlog,
    IReadOnlyList<IssueListItemResponse> Epics,
    DeliveryCapabilities Can);
