namespace Onyx.Contracts.Delivery;

/// <summary>Every visible, non-archived project with its dates, progress and dated epics: the cross-project roadmap.</summary>
public sealed record RoadmapResponse(IReadOnlyList<RoadmapProjectResponse> Projects);
