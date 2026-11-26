namespace Onyx.Contracts.Delivery;

/// <summary>A sprint and its issues in rank order.</summary>
public sealed record SprintBacklogResponse(
    SprintResponse Sprint,
    IReadOnlyList<IssueListItemResponse> Issues);
