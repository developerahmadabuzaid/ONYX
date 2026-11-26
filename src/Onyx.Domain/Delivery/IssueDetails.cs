namespace Onyx.Domain.Delivery;

/// <summary>The editable fields of an issue (status, sprint, hierarchy and rank have their own operations).</summary>
public sealed record IssueDetails(
    string? Title,
    string? Description,
    DeliveryPriority Priority,
    Guid? AssigneeId,
    int? StoryPoints,
    decimal? EstimateHours,
    DateOnly? StartDate,
    DateOnly? DueDate,
    IReadOnlyList<string>? Labels,
    IReadOnlyList<string>? Components,
    string? RepositoryUrl,
    string? BranchName,
    string? PullRequestUrl,
    string? CommitUrl);
