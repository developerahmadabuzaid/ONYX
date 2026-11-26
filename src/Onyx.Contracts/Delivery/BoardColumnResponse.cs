namespace Onyx.Contracts.Delivery;

/// <summary>A board column: one workflow status and its issues in rank order.</summary>
public sealed record BoardColumnResponse(
    WorkflowStatusResponse Status,
    IReadOnlyList<IssueListItemResponse> Issues);
