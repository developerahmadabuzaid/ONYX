using System.Text.RegularExpressions;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>
/// A delivery project in the internal project-management workspace (client software, SaaS products, AI agents, automation,
/// maintenance). It owns its workflow (statuses and allowed transitions) and its members, and hands out issue numbers so issue
/// keys (<c>KEY-1</c>, <c>KEY-2</c>…) are unique and never reused. The key itself never changes once issues exist.
/// </summary>
public sealed partial class DeliveryProject : AuditableEntity, IAuditable
{
    public const int KeyMaxLength = 10;
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 4000;
    public const int ClientNameMaxLength = 120;

    private readonly List<WorkflowStatus> _statuses = [];
    private readonly List<WorkflowTransition> _transitions = [];
    private readonly List<ProjectMember> _members = [];

    private DeliveryProject()
    {
        Key = string.Empty;
        Name = string.Empty;
    }

    /// <summary>2–10 upper-case letters and digits, starting with a letter (AI, WEB, OPS2…).</summary>
    public string Key { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public DeliveryProjectType Type { get; private set; }

    public DeliveryClassification Classification { get; private set; }

    public string? ClientName { get; private set; }

    public DeliveryProjectStatus Status { get; private set; }

    public DeliveryPriority Priority { get; private set; }

    public Guid OwnerId { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? TargetDate { get; private set; }

    public string? RepositoryUrl { get; private set; }

    /// <summary>The number the next issue receives. Only grows, so deleted issues never free their key.</summary>
    public int NextIssueNumber { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyCollection<WorkflowStatus> Statuses => _statuses.AsReadOnly();

    public IReadOnlyCollection<WorkflowTransition> Transitions => _transitions.AsReadOnly();

    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    public AuditCategory AuditCategory => AuditCategory.Delivery;

    public static bool IsValidKey(string? key) => key is not null && KeyPattern().IsMatch(key);

    /// <summary>Creates the project with the default workflow; the owner becomes its first Admin member.</summary>
    public static Result<DeliveryProject> Create(string? key, Guid ownerId, DeliveryProjectDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var cleanKey = key?.Trim().ToUpperInvariant();
        if (!IsValidKey(cleanKey))
        {
            return Error.Validation("Delivery.InvalidProjectKey", "The key is 2–10 upper-case letters or digits and starts with a letter (for example AI or WEB).");
        }

        if (ownerId == Guid.Empty)
        {
            return Error.Validation("Delivery.OwnerRequired", "A project needs an owner.");
        }

        var project = new DeliveryProject { Key = cleanKey!, OwnerId = ownerId, NextIssueNumber = 1 };
        var applied = project.Update(details);
        if (applied.IsFailure)
        {
            return applied.Error!;
        }

        project.AddDefaultWorkflow();
        project._members.Add(new ProjectMember(project.Id, ownerId, ProjectRole.Admin, now));
        return project;
    }

    public Result Update(DeliveryProjectDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var name = DeliveryText.Required(details.Name, NameMaxLength, "Delivery.InvalidProjectName", $"Enter the project name (at most {NameMaxLength} characters).");
        if (name.IsFailure)
        {
            return name.Error!;
        }

        var description = DeliveryText.Optional(details.Description, DescriptionMaxLength, "Delivery.InvalidDescription", $"The description is at most {DescriptionMaxLength} characters.");
        if (description.IsFailure)
        {
            return description.Error!;
        }

        var client = DeliveryText.Optional(details.ClientName, ClientNameMaxLength, "Delivery.InvalidClientName", $"The client name is at most {ClientNameMaxLength} characters.");
        if (client.IsFailure)
        {
            return client.Error!;
        }

        var repository = DeliveryText.OptionalUrl(details.RepositoryUrl, "The repository URL");
        if (repository.IsFailure)
        {
            return repository.Error!;
        }

        if (details.StartDate is { } start && details.TargetDate is { } target && target < start)
        {
            return Error.Validation("Delivery.InvalidProjectDates", "The target date cannot be before the start date.");
        }

        if (details.Classification == DeliveryClassification.Client && client.Value is null)
        {
            return Error.Validation("Delivery.ClientNameRequired", "Name the client for a client project.");
        }

        Name = name.Value;
        Description = description.Value;
        Type = details.Type;
        Classification = details.Classification;
        ClientName = details.Classification == DeliveryClassification.Client ? client.Value : null;
        Status = details.Status;
        Priority = details.Priority;
        StartDate = details.StartDate;
        TargetDate = details.TargetDate;
        RepositoryUrl = repository.Value;
        return Result.Success();
    }

    public int AllocateIssueNumber() => NextIssueNumber++;

    public ProjectRole? RoleOf(Guid userId) => _members.FirstOrDefault(member => member.UserId == userId)?.Role;

    public WorkflowStatus? FindStatus(Guid statusId) => _statuses.FirstOrDefault(status => status.Id == statusId);

    /// <summary>The first status in workflow order (where new issues start).</summary>
    public WorkflowStatus InitialStatus => _statuses.OrderBy(status => status.Order).First();

    public bool CanTransition(Guid fromStatusId, Guid toStatusId) =>
        fromStatusId == toStatusId || _transitions.Any(transition => transition.FromStatusId == fromStatusId && transition.ToStatusId == toStatusId);

    public Result AddMember(Guid userId, ProjectRole role, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            return Error.Validation("Delivery.MemberRequired", "Choose a user.");
        }

        if (_members.Any(member => member.UserId == userId))
        {
            return Error.Conflict("Delivery.AlreadyMember", "This user is already a member of the project.");
        }

        _members.Add(new ProjectMember(Id, userId, role, now));
        return Result.Success();
    }

    public Result ChangeMemberRole(Guid userId, ProjectRole role)
    {
        var member = _members.FirstOrDefault(candidate => candidate.UserId == userId);
        if (member is null)
        {
            return Error.NotFound("Delivery.MemberNotFound", "This user is not a member of the project.");
        }

        if (member.Role == ProjectRole.Admin && role != ProjectRole.Admin && _members.Count(candidate => candidate.Role == ProjectRole.Admin) == 1)
        {
            return Error.Conflict("Delivery.LastProjectAdmin", "A project keeps at least one Admin member.");
        }

        member.ChangeRole(role);
        return Result.Success();
    }

    public Result RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(candidate => candidate.UserId == userId);
        if (member is null)
        {
            return Error.NotFound("Delivery.MemberNotFound", "This user is not a member of the project.");
        }

        if (member.Role == ProjectRole.Admin && _members.Count(candidate => candidate.Role == ProjectRole.Admin) == 1)
        {
            return Error.Conflict("Delivery.LastProjectAdmin", "A project keeps at least one Admin member.");
        }

        _members.Remove(member);
        return Result.Success();
    }

    public Result SetWipLimit(Guid statusId, int? limit) =>
        FindStatus(statusId) is { } status ? status.SetWipLimit(limit) : Error.NotFound("Delivery.StatusNotFound", "Workflow status not found.");

    /// <summary>
    /// Backlog → To Do → In Progress → In Review → Testing → Done. Work may step back one stage, review and testing may send work
    /// back to In Progress, open work may return to the Backlog, and Done may be reopened to To Do.
    /// </summary>
    private void AddDefaultWorkflow()
    {
        var backlog = AddStatus("Backlog", "backlog", StatusCategory.ToDo, 0);
        var todo = AddStatus("To Do", "todo", StatusCategory.ToDo, 1);
        var progress = AddStatus("In Progress", "in-progress", StatusCategory.InProgress, 2);
        var review = AddStatus("In Review", "in-review", StatusCategory.InProgress, 3);
        var testing = AddStatus("Testing", "testing", StatusCategory.InProgress, 4);
        var done = AddStatus("Done", "done", StatusCategory.Done, 5);

        (WorkflowStatus From, WorkflowStatus To)[] moves =
        [
            (backlog, todo), (todo, backlog),
            (todo, progress), (progress, todo),
            (progress, review), (review, progress),
            (review, testing), (testing, review),
            (testing, progress), (testing, done),
            (progress, backlog), (review, backlog),
            (done, todo),
        ];
        foreach (var (from, to) in moves)
        {
            _transitions.Add(new WorkflowTransition(Id, from.Id, to.Id));
        }
    }

    private WorkflowStatus AddStatus(string name, string key, StatusCategory category, int order)
    {
        var status = new WorkflowStatus(Id, name, key, category, order);
        _statuses.Add(status);
        return status;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
