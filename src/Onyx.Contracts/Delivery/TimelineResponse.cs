namespace Onyx.Contracts.Delivery;

/// <summary>
/// A project's schedule: dated issues and epics, "blocks" links between them, sprints, and the project's own dates as
/// milestones. Issues without any date are counted in <see cref="UnscheduledCount"/> but not listed.
/// </summary>
public sealed record TimelineResponse(
    Guid ProjectId,
    string ProjectKey,
    string ProjectName,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    IReadOnlyList<TimelineItemResponse> Items,
    IReadOnlyList<TimelineDependencyResponse> Dependencies,
    IReadOnlyList<SprintResponse> Sprints,
    int UnscheduledCount,
    DeliveryCapabilities Can);
