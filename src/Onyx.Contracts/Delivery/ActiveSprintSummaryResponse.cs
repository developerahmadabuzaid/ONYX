namespace Onyx.Contracts.Delivery;

/// <summary>An active sprint on the dashboard.</summary>
public sealed record ActiveSprintSummaryResponse(
    Guid ProjectId,
    string ProjectKey,
    SprintResponse Sprint);
