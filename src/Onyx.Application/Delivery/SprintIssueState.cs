namespace Onyx.Application.Delivery;

/// <summary>An issue's current sprint, status and points, the starting point for rebuilding its past from the history.</summary>
public sealed record SprintIssueState(Guid IssueId, Guid? SprintId, Guid StatusId, int? StoryPoints, DateTimeOffset CreatedAtUtc);
