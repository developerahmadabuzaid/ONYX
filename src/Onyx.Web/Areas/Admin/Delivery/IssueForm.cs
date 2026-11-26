using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Issue create/edit form fields; labels and components are comma-separated.</summary>
public sealed class IssueForm
{
    public Guid? ProjectId { get; set; }

    public string? Type { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Priority { get; set; }

    public Guid? AssigneeId { get; set; }

    public int? StoryPoints { get; set; }

    public decimal? EstimateHours { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? Labels { get; set; }

    public string? Components { get; set; }

    public Guid? ParentId { get; set; }

    public Guid? EpicId { get; set; }

    public Guid? SprintId { get; set; }

    public string? RepositoryUrl { get; set; }

    public string? BranchName { get; set; }

    public string? PullRequestUrl { get; set; }

    public string? CommitUrl { get; set; }

    public uint? Version { get; set; }

    public static IssueForm From(IssueDetailResponse issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        var summary = issue.Summary;
        return new IssueForm
        {
            ProjectId = summary.ProjectId,
            Type = summary.Type,
            Title = summary.Title,
            Description = issue.Description,
            Priority = summary.Priority,
            AssigneeId = summary.AssigneeId,
            StoryPoints = summary.StoryPoints,
            EstimateHours = issue.EstimateHours,
            StartDate = issue.StartDate,
            DueDate = summary.DueDate,
            Labels = string.Join(", ", summary.Labels),
            Components = string.Join(", ", issue.Components),
            ParentId = summary.ParentId,
            EpicId = summary.EpicId,
            SprintId = summary.SprintId,
            RepositoryUrl = issue.RepositoryUrl,
            BranchName = issue.BranchName,
            PullRequestUrl = issue.PullRequestUrl,
            CommitUrl = issue.CommitUrl,
            Version = summary.Version,
        };
    }

    public SaveIssueRequest ToRequest() =>
        new(ProjectId, Type, Title, Description, Priority, AssigneeId, StoryPoints, EstimateHours, StartDate, DueDate, Split(Labels), Split(Components),
            ParentId, EpicId, SprintId, RepositoryUrl, BranchName, PullRequestUrl, CommitUrl, Version);

    private static IReadOnlyList<string> Split(string? value) =>
        (value ?? string.Empty).Split([',', '\u060C'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
