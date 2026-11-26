using System.Globalization;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>
/// A work item in a delivery project (epic, story, task, bug, subtask, feature, improvement or technical debt).
/// <para>
/// Hierarchy: a <see cref="IssueType.Subtask"/> always has a parent (a standard issue of the same project); standard issues may
/// belong to an epic. Epics and subtasks keep their type, so the hierarchy can never become inconsistent.
/// </para>
/// <para>
/// <see cref="Rank"/> orders issues within the backlog and within a board column; the service rewrites ranks of the affected list
/// in one transaction when an issue is moved. Status changes are validated against the project's workflow by the caller.
/// </para>
/// </summary>
public sealed class Issue : AuditableEntity, IAuditable
{
    public const int KeyMaxLength = 20;
    public const int TitleMaxLength = 255;
    public const int DescriptionMaxLength = 20000;
    public const int BranchMaxLength = 200;
    public const int MaxStoryPoints = 100;
    public const decimal MaxEstimateHours = 1000m;

    private Issue()
    {
        Key = string.Empty;
        Title = string.Empty;
        Labels = [];
        Components = [];
    }

    public Guid ProjectId { get; private set; }

    public int Number { get; private set; }

    /// <summary>Project key and number, e.g. AI-12. Unique, never reused.</summary>
    public string Key { get; private set; }

    public IssueType Type { get; private set; }

    public string Title { get; private set; }

    /// <summary>Plain text (line breaks kept). Rendered encoded, never as HTML.</summary>
    public string? Description { get; private set; }

    public Guid StatusId { get; private set; }

    public DeliveryPriority Priority { get; private set; }

    public Guid ReporterId { get; private set; }

    public Guid? AssigneeId { get; private set; }

    /// <summary>The parent of a subtask.</summary>
    public Guid? ParentId { get; private set; }

    public Guid? EpicId { get; private set; }

    public Guid? SprintId { get; private set; }

    public int? StoryPoints { get; private set; }

    public decimal? EstimateHours { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public List<string> Labels { get; private set; }

    public List<string> Components { get; private set; }

    public long Rank { get; private set; }

    /// <summary>When the issue last entered a Done status; cleared when it is reopened.</summary>
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public string? RepositoryUrl { get; private set; }

    public string? BranchName { get; private set; }

    public string? PullRequestUrl { get; private set; }

    public string? CommitUrl { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Delivery;

    public bool IsEpic => Type == IssueType.Epic;

    public bool IsSubtask => Type == IssueType.Subtask;

    /// <summary>Creates an issue in the project's first status. Hierarchy is set separately with <see cref="SetHierarchy"/>.</summary>
    public static Result<Issue> Create(DeliveryProject project, IssueType type, Guid reporterId, IssueDetails details, long rank)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(details);
        if (!Enum.IsDefined(type))
        {
            return Error.Validation("Delivery.InvalidIssueType", "Choose a valid issue type.");
        }

        var number = project.AllocateIssueNumber();
        var issue = new Issue
        {
            ProjectId = project.Id,
            Number = number,
            Key = string.Create(CultureInfo.InvariantCulture, $"{project.Key}-{number}"),
            Type = type,
            StatusId = project.InitialStatus.Id,
            ReporterId = reporterId,
            Rank = rank,
        };
        var applied = issue.Update(details);
        return applied.IsFailure ? applied.Error! : issue;
    }

    /// <summary>Applies the editable fields and returns what changed (for the issue history).</summary>
    public Result<IReadOnlyList<IssueChange>> Update(IssueDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var title = DeliveryText.Required(details.Title, TitleMaxLength, "Delivery.InvalidTitle", $"Enter a title (at most {TitleMaxLength} characters).");
        if (title.IsFailure)
        {
            return title.Error!;
        }

        var description = DeliveryText.Optional(details.Description, DescriptionMaxLength, "Delivery.InvalidDescription", $"The description is at most {DescriptionMaxLength} characters.");
        if (description.IsFailure)
        {
            return description.Error!;
        }

        if (details.StoryPoints is < 0 or > MaxStoryPoints)
        {
            return Error.Validation("Delivery.InvalidStoryPoints", $"Story points are between 0 and {MaxStoryPoints}.");
        }

        if (details.EstimateHours is < 0 or > MaxEstimateHours)
        {
            return Error.Validation("Delivery.InvalidEstimate", $"The estimate is between 0 and {MaxEstimateHours} hours.");
        }

        if (details.StartDate is { } start && details.DueDate is { } due && due < start)
        {
            return Error.Validation("Delivery.InvalidIssueDates", "The due date cannot be before the start date.");
        }

        var labels = DeliveryText.Labels(details.Labels);
        if (labels.IsFailure)
        {
            return labels.Error!;
        }

        var components = DeliveryText.Labels(details.Components);
        if (components.IsFailure)
        {
            return Error.Validation("Delivery.InvalidComponents", "Components follow the same rules as labels.");
        }

        var branch = DeliveryText.Optional(details.BranchName, BranchMaxLength, "Delivery.InvalidBranch", $"The branch name is at most {BranchMaxLength} characters.");
        var repository = DeliveryText.OptionalUrl(details.RepositoryUrl, "The repository URL");
        var pullRequest = DeliveryText.OptionalUrl(details.PullRequestUrl, "The pull request URL");
        var commit = DeliveryText.OptionalUrl(details.CommitUrl, "The commit URL");
        foreach (var check in new Result[] { branch, repository, pullRequest, commit })
        {
            if (check.IsFailure)
            {
                return check.Error!;
            }
        }

        var changes = new List<IssueChange>();
        Track(changes, "title", Title, title.Value);
        Track(changes, "description", Description, description.Value);
        Track(changes, "priority", Priority.ToString(), details.Priority.ToString());
        Track(changes, "assignee", AssigneeId?.ToString(), details.AssigneeId?.ToString());
        Track(changes, "storyPoints", Format(StoryPoints), Format(details.StoryPoints));
        Track(changes, "estimateHours", Format(EstimateHours), Format(details.EstimateHours));
        Track(changes, "startDate", FormatDate(StartDate), FormatDate(details.StartDate));
        Track(changes, "dueDate", FormatDate(DueDate), FormatDate(details.DueDate));
        Track(changes, "labels", string.Join(", ", Labels), string.Join(", ", labels.Value));
        Track(changes, "components", string.Join(", ", Components), string.Join(", ", components.Value));
        Track(changes, "repositoryUrl", RepositoryUrl, repository.Value);
        Track(changes, "branchName", BranchName, branch.Value);
        Track(changes, "pullRequestUrl", PullRequestUrl, pullRequest.Value);
        Track(changes, "commitUrl", CommitUrl, commit.Value);

        Title = title.Value;
        Description = description.Value;
        Priority = details.Priority;
        AssigneeId = details.AssigneeId;
        StoryPoints = details.StoryPoints;
        EstimateHours = details.EstimateHours;
        StartDate = details.StartDate;
        DueDate = details.DueDate;
        Labels = labels.Value;
        Components = components.Value;
        RepositoryUrl = repository.Value;
        BranchName = branch.Value;
        PullRequestUrl = pullRequest.Value;
        CommitUrl = commit.Value;
        return changes;
    }

    /// <summary>Changes the type between standard types. Epics and subtasks keep their type (their relationships depend on it).</summary>
    public Result<IssueChange?> ChangeType(IssueType type)
    {
        if (type == Type)
        {
            return Result.Success<IssueChange?>(null);
        }

        if (!Enum.IsDefined(type) || IsEpic || IsSubtask || type is IssueType.Epic or IssueType.Subtask)
        {
            return Error.Validation("Delivery.TypeChangeNotAllowed", "Epics and subtasks keep their type; other issues can switch between standard types.");
        }

        var change = new IssueChange("type", Type.ToString(), type.ToString());
        Type = type;
        return change;
    }

    /// <summary>
    /// Sets the parent (subtasks only) and the epic (standard issues only). <paramref name="parent"/> and <paramref name="epic"/>
    /// are the loaded issues, or null to clear.
    /// </summary>
    public Result SetHierarchy(Issue? parent, Issue? epic)
    {
        if (IsSubtask)
        {
            if (parent is null)
            {
                return Error.Validation("Delivery.SubtaskNeedsParent", "A subtask needs a parent issue.");
            }

            if (parent.ProjectId != ProjectId || parent.IsEpic || parent.IsSubtask || parent.Id == Id)
            {
                return Error.Validation("Delivery.InvalidParent", "A subtask's parent is a standard issue of the same project (not an epic or another subtask).");
            }

            if (epic is not null)
            {
                return Error.Validation("Delivery.SubtaskEpic", "A subtask belongs to its parent's epic.");
            }

            ParentId = parent.Id;
            EpicId = null;
            return Result.Success();
        }

        if (parent is not null)
        {
            return Error.Validation("Delivery.ParentOnlyForSubtasks", "Only subtasks have a parent issue; use the epic to group other issues.");
        }

        if (epic is not null && (IsEpic || !epic.IsEpic || epic.ProjectId != ProjectId))
        {
            return Error.Validation("Delivery.InvalidEpic", "Choose an epic of the same project (epics cannot belong to another epic).");
        }

        ParentId = null;
        EpicId = epic?.Id;
        return Result.Success();
    }

    /// <summary>Moves to a status the caller has already validated against the workflow; tracks resolution time.</summary>
    public IssueChange? MoveTo(WorkflowStatus status, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (status.Id == StatusId)
        {
            return null;
        }

        var previous = StatusId;
        StatusId = status.Id;
        ResolvedAtUtc = status.Category == StatusCategory.Done ? now : null;
        return new IssueChange("status", previous.ToString(), status.Id.ToString());
    }

    /// <summary>Moves the start and due dates (calendar and timeline scheduling). Returns what changed for the history.</summary>
    public Result<IReadOnlyList<IssueChange>> Reschedule(DateOnly? startDate, DateOnly? dueDate)
    {
        if (startDate is { } start && dueDate is { } due && due < start)
        {
            return Error.Validation("Delivery.InvalidIssueDates", "The due date cannot be before the start date.");
        }

        var changes = new List<IssueChange>();
        Track(changes, "startDate", FormatDate(StartDate), FormatDate(startDate));
        Track(changes, "dueDate", FormatDate(DueDate), FormatDate(dueDate));
        StartDate = startDate;
        DueDate = dueDate;
        return changes;
    }

    public IssueChange? SetSprint(Guid? sprintId)
    {
        if (SprintId == sprintId)
        {
            return null;
        }

        var change = new IssueChange("sprint", SprintId?.ToString(), sprintId?.ToString());
        SprintId = sprintId;
        return change;
    }

    public void SetRank(long rank) => Rank = rank;

    private static void Track(List<IssueChange> changes, string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal))
        {
            changes.Add(new IssueChange(field, oldValue, newValue));
        }
    }

    private static string? FormatDate(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Format<T>(T? value)
        where T : struct, IFormattable =>
        value?.ToString(null, CultureInfo.InvariantCulture);
}
