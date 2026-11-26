namespace Onyx.Contracts.Delivery;

/// <summary>Share of a project's issues that are done (by count; no estimate involved).</summary>
public sealed record ProjectProgressResponse(
    Guid ProjectId,
    string ProjectKey,
    string ProjectName,
    int TotalIssues,
    int DoneIssues,
    DateOnly? TargetDate);
