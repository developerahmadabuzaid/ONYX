using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>
/// A time-boxed iteration of a project. Planned → Active → Completed, or Cancelled before it completes. Starting records what was
/// committed and completing records what was done, so sprint reports and velocity come from real snapshots, never estimates.
/// Only one sprint per project can be active (checked by the service and enforced by a partial unique index).
/// </summary>
public sealed class Sprint : AuditableEntity, IAuditable
{
    public const int NameMaxLength = 80;
    public const int GoalMaxLength = 500;
    public const int RetrospectiveMaxLength = 4000;
    public const int MaxLengthDays = 60;

    private Sprint()
    {
        Name = string.Empty;
    }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; }

    public string? Goal { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public SprintState State { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public int? CommittedIssueCount { get; private set; }

    public int? CommittedPoints { get; private set; }

    public int? CompletedIssueCount { get; private set; }

    public int? CompletedPoints { get; private set; }

    public uint Version { get; private set; }

    /// <summary>Retrospective: what went well (plain text). Recorded once the sprint has started.</summary>
    public string? RetroWentWell { get; private set; }

    /// <summary>Retrospective: what to improve (plain text).</summary>
    public string? RetroToImprove { get; private set; }

    /// <summary>Retrospective: agreed actions (plain text).</summary>
    public string? RetroActionItems { get; private set; }

    public DateTimeOffset? RetroUpdatedAtUtc { get; private set; }

    public Guid? RetroUpdatedById { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Delivery;

    public bool IsOpen => State is SprintState.Planned or SprintState.Active;

    public static Result<Sprint> Create(Guid projectId, string? name, string? goal, DateOnly? startDate, DateOnly? endDate)
    {
        var sprint = new Sprint { ProjectId = projectId, State = SprintState.Planned };
        var planned = sprint.Plan(name, goal, startDate, endDate);
        return planned.IsFailure ? planned.Error! : sprint;
    }

    /// <summary>Name, goal and dates can change while the sprint is open; dates are required to start it.</summary>
    public Result Plan(string? name, string? goal, DateOnly? startDate, DateOnly? endDate)
    {
        if (!IsOpen)
        {
            return Error.Conflict("Delivery.SprintClosed", "A completed or cancelled sprint cannot be changed.");
        }

        var cleanName = DeliveryText.Required(name, NameMaxLength, "Delivery.InvalidSprintName", $"Name the sprint (at most {NameMaxLength} characters).");
        if (cleanName.IsFailure)
        {
            return cleanName.Error!;
        }

        var cleanGoal = DeliveryText.Optional(goal, GoalMaxLength, "Delivery.InvalidSprintGoal", $"The goal is at most {GoalMaxLength} characters.");
        if (cleanGoal.IsFailure)
        {
            return cleanGoal.Error!;
        }

        var dates = ValidateDates(startDate, endDate);
        if (dates.IsFailure)
        {
            return dates;
        }

        Name = cleanName.Value;
        Goal = cleanGoal.Value;
        StartDate = startDate;
        EndDate = endDate;
        return Result.Success();
    }

    public Result Start(int committedIssueCount, int committedPoints, DateTimeOffset now)
    {
        if (State != SprintState.Planned)
        {
            return Error.Conflict("Delivery.SprintNotPlanned", "Only a planned sprint can be started.");
        }

        if (StartDate is null || EndDate is null)
        {
            return Error.Validation("Delivery.SprintDatesRequired", "Set the start and end dates before starting the sprint.");
        }

        if (committedIssueCount == 0)
        {
            return Error.Validation("Delivery.SprintEmpty", "Add at least one issue before starting the sprint.");
        }

        State = SprintState.Active;
        StartedAtUtc = now;
        CommittedIssueCount = committedIssueCount;
        CommittedPoints = committedPoints;
        return Result.Success();
    }

    public Result Complete(int completedIssueCount, int completedPoints, DateTimeOffset now)
    {
        if (State != SprintState.Active)
        {
            return Error.Conflict("Delivery.SprintNotActive", "Only an active sprint can be completed.");
        }

        State = SprintState.Completed;
        ClosedAtUtc = now;
        CompletedIssueCount = completedIssueCount;
        CompletedPoints = completedPoints;
        return Result.Success();
    }

    /// <summary>Records the team's retrospective notes. Possible once the sprint has started (also after it closed).</summary>
    public Result RecordRetrospective(string? wentWell, string? toImprove, string? actionItems, Guid userId, DateTimeOffset now)
    {
        if (State == SprintState.Planned)
        {
            return Error.Conflict("Delivery.RetrospectiveTooEarly", "A retrospective can be recorded once the sprint has started.");
        }

        var message = $"Each retrospective section is at most {RetrospectiveMaxLength} characters.";
        var well = DeliveryText.Optional(wentWell, RetrospectiveMaxLength, "Delivery.InvalidRetrospective", message);
        var improve = DeliveryText.Optional(toImprove, RetrospectiveMaxLength, "Delivery.InvalidRetrospective", message);
        var actions = DeliveryText.Optional(actionItems, RetrospectiveMaxLength, "Delivery.InvalidRetrospective", message);
        foreach (var check in new Result[] { well, improve, actions })
        {
            if (check.IsFailure)
            {
                return check;
            }
        }

        RetroWentWell = well.Value;
        RetroToImprove = improve.Value;
        RetroActionItems = actions.Value;
        RetroUpdatedAtUtc = now;
        RetroUpdatedById = userId;
        return Result.Success();
    }

    public Result Cancel(DateTimeOffset now)
    {
        if (!IsOpen)
        {
            return Error.Conflict("Delivery.SprintClosed", "This sprint is already closed.");
        }

        State = SprintState.Cancelled;
        ClosedAtUtc = now;
        return Result.Success();
    }

    private static Result ValidateDates(DateOnly? startDate, DateOnly? endDate)
    {
        if (startDate is null != endDate is null)
        {
            return Error.Validation("Delivery.InvalidSprintDates", "Set both the start and the end date, or neither.");
        }

        if (startDate is { } start && endDate is { } end && (end < start || end.DayNumber - start.DayNumber > MaxLengthDays))
        {
            return Error.Validation("Delivery.InvalidSprintDates", $"The end date is on or after the start date, at most {MaxLengthDays} days later.");
        }

        return Result.Success();
    }
}
