namespace Onyx.Contracts.Delivery;

/// <summary>A project with its workflow, members and the caller's role (null for workspace administrators who are not members).</summary>
public sealed record DeliveryProjectResponse(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    string Type,
    string Classification,
    string? ClientName,
    string Status,
    string Priority,
    Guid OwnerId,
    string OwnerName,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    string? RepositoryUrl,
    string? MyRole,
    DeliveryCapabilities Can,
    IReadOnlyList<WorkflowStatusResponse> Statuses,
    IReadOnlyList<WorkflowTransitionResponse> Transitions,
    IReadOnlyList<ProjectMemberResponse> Members,
    int TotalIssues,
    int DoneIssues,
    uint Version);
