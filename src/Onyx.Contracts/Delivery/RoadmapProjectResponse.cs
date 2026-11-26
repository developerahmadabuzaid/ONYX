namespace Onyx.Contracts.Delivery;

/// <summary>A project on the roadmap.</summary>
public sealed record RoadmapProjectResponse(
    Guid Id,
    string Key,
    string Name,
    string Status,
    string Health,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    int TotalIssues,
    int DoneIssues,
    IReadOnlyList<TimelineItemResponse> Epics);
