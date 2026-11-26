namespace Onyx.Application.Delivery;

/// <summary>A recorded change of an issue's sprint, status or story points (values as stored in the issue history).</summary>
public sealed record SprintHistoryEvent(Guid IssueId, string Field, string? OldValue, string? NewValue, DateTimeOffset AtUtc);
