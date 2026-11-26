namespace Onyx.Contracts.Delivery;

/// <summary>A project board. With an active sprint the board shows that sprint (Scrum); otherwise every issue (Kanban).</summary>
public sealed record BoardResponse(
    Guid ProjectId,
    string ProjectKey,
    string ProjectName,
    SprintResponse? ActiveSprint,
    IReadOnlyList<BoardColumnResponse> Columns,
    IReadOnlyList<WorkflowTransitionResponse> Transitions,
    IReadOnlyList<ProjectMemberResponse> Members,
    DeliveryCapabilities Can,
    uint ProjectVersion);
