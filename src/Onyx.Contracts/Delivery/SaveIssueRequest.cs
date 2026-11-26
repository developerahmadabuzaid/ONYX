namespace Onyx.Contracts.Delivery;

/// <summary>Create (ProjectId and Type required) or update (Version required) an issue.</summary>
public sealed record SaveIssueRequest(
    Guid? ProjectId,
    string? Type,
    string? Title,
    string? Description,
    string? Priority,
    Guid? AssigneeId,
    int? StoryPoints,
    decimal? EstimateHours,
    DateOnly? StartDate,
    DateOnly? DueDate,
    IReadOnlyList<string>? Labels,
    IReadOnlyList<string>? Components,
    Guid? ParentId,
    Guid? EpicId,
    Guid? SprintId,
    string? RepositoryUrl,
    string? BranchName,
    string? PullRequestUrl,
    string? CommitUrl,
    uint? Version);
