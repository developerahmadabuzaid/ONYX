namespace Onyx.Contracts.Delivery;

/// <summary>
/// A sprint with its burndown and retrospective. The burndown is rebuilt from the issue history: for each day, the story points
/// (and issues) that were in the sprint and not done at the end of that day, up to today or the day the sprint closed.
/// </summary>
public sealed record SprintReportResponse(
    Guid ProjectId,
    string ProjectKey,
    string ProjectName,
    SprintResponse Sprint,
    IReadOnlyList<BurndownPointResponse> Burndown,
    IReadOnlyList<IssueListItemResponse> Issues,
    SprintRetrospectiveResponse Retrospective,
    DeliveryCapabilities Can);
