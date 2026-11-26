namespace Onyx.Contracts.Delivery;

/// <summary>A closed sprint's committed (at start) and completed (at close) work, as the sprint recorded it.</summary>
public sealed record SprintVelocityResponse(
    Guid SprintId,
    string Name,
    int CommittedPoints,
    int CompletedPoints,
    int CommittedIssues,
    int CompletedIssues,
    DateTimeOffset? ClosedAtUtc);
