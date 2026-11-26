using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Contracts.Delivery;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

/// <summary>
/// Sprint lifecycle (project managers and admins): plan, start (one active sprint per project; the committed scope is recorded),
/// complete (the completed scope is recorded and unfinished issues move to another open sprint or the backlog) and cancel.
/// </summary>
public sealed class SprintService(
    IDeliveryProjectRepository projects,
    ISprintRepository sprints,
    IIssueRepository issues,
    IDeliveryReadModel readModel,
    DeliveryAccess access,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private static readonly Error SprintNotFound = Error.NotFound("Delivery.SprintNotFound", "Sprint not found.");

    public async Task<Result<IReadOnlyList<SprintResponse>>> ListAsync(Guid projectId, bool includeClosed, CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        var project = await projects.GetAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return DeliveryAccess.ProjectNotFound;
        }

        var can = DeliveryAccess.For(viewer, project);
        return can.IsFailure ? can.Error! : Result.Success(await readModel.SprintsAsync(projectId, includeClosed, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<SprintResponse>> CreateAsync(Guid projectId, SaveSprintRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await AuthorizeAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return allowed.Error!;
        }

        var created = Sprint.Create(projectId, request.Name, request.Goal, request.StartDate, request.EndDate);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        sprints.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ResponseAsync(created.Value.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<SprintResponse>> PlanAsync(Guid sprintId, SaveSprintRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is not { } version)
        {
            return Error.Validation("Delivery.VersionRequired", "Reload the sprint and try again.");
        }

        var sprint = await LoadAsync(sprintId, cancellationToken).ConfigureAwait(false);
        if (sprint.IsFailure)
        {
            return sprint.Error!;
        }

        sprints.SetExpectedVersion(sprint.Value, version);
        var planned = sprint.Value.Plan(request.Name, request.Goal, request.StartDate, request.EndDate);
        if (planned.IsFailure)
        {
            return planned.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ResponseAsync(sprintId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<SprintResponse>> StartAsync(Guid sprintId, uint version, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(sprintId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var sprint = loaded.Value;
        if (await sprints.ActiveSprintIdAsync(sprint.ProjectId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Error.Conflict("Delivery.SprintAlreadyActive", "Complete the active sprint before starting another one.");
        }

        sprints.SetExpectedVersion(sprint, version);
        var scope = await issues.ListInSprintAsync(sprint.Id, cancellationToken).ConfigureAwait(false);
        var committed = scope.Where(issue => !issue.IsSubtask).ToList();
        var started = sprint.Start(committed.Count, committed.Sum(issue => issue.StoryPoints ?? 0), timeProvider.GetUtcNow());
        if (started.IsFailure)
        {
            return started.Error!;
        }

        auditTrail.Record(AuditAction.DeliverySprintStarted, AuditCategory.Delivery, nameof(Sprint), sprint.Id.ToString(), new { name = sprint.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ResponseAsync(sprintId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<SprintResponse>> CompleteAsync(Guid sprintId, CloseSprintRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await CloseAsync(sprintId, request, complete: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<SprintResponse>> CancelAsync(Guid sprintId, CloseSprintRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await CloseAsync(sprintId, request, complete: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<SprintResponse>> CloseAsync(Guid sprintId, CloseSprintRequest request, bool complete, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(sprintId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var sprint = loaded.Value;
        if (request.MoveOpenIssuesToSprintId is { } targetId)
        {
            var target = await sprints.GetAsync(targetId, cancellationToken).ConfigureAwait(false);
            if (target is null || target.ProjectId != sprint.ProjectId || target.Id == sprint.Id || !target.IsOpen)
            {
                return Error.Validation("Delivery.InvalidSprintTarget", "Move unfinished work to another open sprint of this project, or to the backlog.");
            }
        }

        sprints.SetExpectedVersion(sprint, request.Version);
        var done = await issues.DoneStatusIdsAsync(sprint.ProjectId, cancellationToken).ConfigureAwait(false);
        var scope = await issues.ListInSprintAsync(sprint.Id, cancellationToken).ConfigureAwait(false);
        var finished = scope.Where(issue => !issue.IsSubtask && done.Contains(issue.StatusId)).ToList();
        var unfinished = scope.Where(issue => !done.Contains(issue.StatusId)).ToList();
        var now = timeProvider.GetUtcNow();

        var closed = complete ? sprint.Complete(finished.Count, finished.Sum(issue => issue.StoryPoints ?? 0), now) : sprint.Cancel(now);
        if (closed.IsFailure)
        {
            return closed.Error!;
        }

        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        foreach (var issue in unfinished)
        {
            if (issue.SetSprint(request.MoveOpenIssuesToSprintId) is { } change)
            {
                issues.AddHistory(new IssueHistoryEntry(issue.ProjectId, issue.Id, viewer.UserId, change.Field, change.OldValue, change.NewValue, now));
            }
        }

        auditTrail.Record(
            complete ? AuditAction.DeliverySprintCompleted : AuditAction.DeliverySprintCancelled,
            AuditCategory.Delivery,
            nameof(Sprint),
            sprint.Id.ToString(),
            new { name = sprint.Name, moved = unfinished.Count, to = request.MoveOpenIssuesToSprintId });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ResponseAsync(sprintId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The sprint report: scope, burndown rebuilt from the issue history, issues and retrospective. Any project member may view it.</summary>
    public async Task<Result<SprintReportResponse>> ReportAsync(Guid sprintId, CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        var sprint = await sprints.GetAsync(sprintId, cancellationToken).ConfigureAwait(false);
        var project = sprint is null ? null : await projects.GetAsync(sprint.ProjectId, cancellationToken).ConfigureAwait(false);
        if (sprint is null || project is null)
        {
            return SprintNotFound;
        }

        var can = DeliveryAccess.For(viewer, project);
        if (can.IsFailure)
        {
            return SprintNotFound;
        }

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var response = await ResponseAsync(sprintId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<BurndownPointResponse> burndown = [];
        if (sprint.StartedAtUtc is { } startedAt)
        {
            var firstDay = sprint.StartDate ?? DateOnly.FromDateTime(startedAt.UtcDateTime);
            var lastDay = sprint.EndDate ?? today;
            if (sprint.ClosedAtUtc is { } closedAt && DateOnly.FromDateTime(closedAt.UtcDateTime) < lastDay)
            {
                lastDay = DateOnly.FromDateTime(closedAt.UtcDateTime);
            }

            if (today < lastDay)
            {
                lastDay = today;
            }

            var (states, events, doneStatusIds) = await readModel.SprintHistoryAsync(sprintId, project.Id, cancellationToken).ConfigureAwait(false);
            burndown = BurndownCalculator.Calculate(sprintId, firstDay, lastDay, states, events, doneStatusIds, sprint.ClosedAtUtc);
        }

        var issueList = await readModel.BoardIssuesAsync(project.Id, sprintId, today, cancellationToken).ConfigureAwait(false);
        var updatedBy = sprint.RetroUpdatedById is { } userId
            ? (await readModel.DisplayNamesAsync([userId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(userId)
            : null;
        var retrospective = new SprintRetrospectiveResponse(sprint.RetroWentWell, sprint.RetroToImprove, sprint.RetroActionItems, sprint.RetroUpdatedAtUtc, updatedBy);
        return new SprintReportResponse(project.Id, project.Key, project.Name, response, burndown, issueList, retrospective, can.Value);
    }

    /// <summary>Records the retrospective (sprint managers), once the sprint has started.</summary>
    public async Task<Result<SprintReportResponse>> SaveRetrospectiveAsync(Guid sprintId, SaveRetrospectiveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sprint = await LoadAsync(sprintId, cancellationToken).ConfigureAwait(false);
        if (sprint.IsFailure)
        {
            return sprint.Error!;
        }

        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        sprints.SetExpectedVersion(sprint.Value, request.Version);
        var recorded = sprint.Value.RecordRetrospective(request.WentWell, request.ToImprove, request.ActionItems, viewer.UserId, timeProvider.GetUtcNow());
        if (recorded.IsFailure)
        {
            return recorded.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ReportAsync(sprintId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<Sprint>> LoadAsync(Guid sprintId, CancellationToken cancellationToken)
    {
        var sprint = await sprints.GetAsync(sprintId, cancellationToken).ConfigureAwait(false);
        if (sprint is null)
        {
            return SprintNotFound;
        }

        var allowed = await AuthorizeAsync(sprint.ProjectId, cancellationToken).ConfigureAwait(false);
        return allowed.IsFailure ? (allowed.Error == DeliveryAccess.ProjectNotFound ? SprintNotFound : allowed.Error!) : sprint;
    }

    private async Task<Result> AuthorizeAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        var project = await projects.GetAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return DeliveryAccess.ProjectNotFound;
        }

        var can = DeliveryAccess.For(viewer, project);
        if (can.IsFailure)
        {
            return can.Error!;
        }

        return can.Value.ManageSprints ? Result.Success() : DeliveryAccess.Forbidden;
    }

    private async Task<SprintResponse> ResponseAsync(Guid sprintId, CancellationToken cancellationToken) =>
        await readModel.SprintAsync(sprintId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A saved sprint must be readable.");
}
