using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;
using Onyx.Domain.Common;

namespace Onyx.Application.Delivery;

/// <summary>Read screens of the delivery workspace: issue search and My Work, project board, backlog and the dashboard.</summary>
public sealed class DeliveryQueryService(
    IDeliveryProjectRepository projects,
    ISprintRepository sprints,
    IDeliveryReadModel readModel,
    DeliveryAccess access,
    TimeProvider timeProvider)
{
    public const int MaxPageSize = 100;

    public async Task<PagedResult<IssueListItemResponse>> SearchAsync(IssueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        var safe = query with { Page = Math.Max(1, query.Page), PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize), Text = query.Text?.Trim() };
        return await readModel.SearchIssuesAsync(viewer, safe, Today(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<BoardResponse>> BoardAsync(Guid projectId, CancellationToken cancellationToken)
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

        var activeSprintId = await sprints.ActiveSprintIdAsync(projectId, cancellationToken).ConfigureAwait(false);
        var activeSprint = activeSprintId is { } id ? await readModel.SprintAsync(id, cancellationToken).ConfigureAwait(false) : null;
        var issues = await readModel.BoardIssuesAsync(projectId, activeSprintId, Today(), cancellationToken).ConfigureAwait(false);
        var names = await readModel.DisplayNamesAsync(project.Members.Select(member => member.UserId).ToList(), cancellationToken).ConfigureAwait(false);
        var columns = DeliveryProjectService.Statuses(project)
            .Select(status => new BoardColumnResponse(status, issues.Where(issue => issue.StatusId == status.Id).ToList()))
            .ToList();
        return new BoardResponse(
            project.Id,
            project.Key,
            project.Name,
            activeSprint,
            columns,
            project.Transitions.Select(transition => new WorkflowTransitionResponse(transition.FromStatusId, transition.ToStatusId)).ToList(),
            project.Members
                .Select(member => new ProjectMemberResponse(member.UserId, names.GetValueOrDefault(member.UserId, string.Empty), member.Role.ToString()))
                .OrderBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            can.Value,
            project.Version);
    }

    public async Task<Result<BacklogResponse>> BacklogAsync(Guid projectId, CancellationToken cancellationToken)
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

        var (sprintLists, backlog, epics) = await readModel.BacklogAsync(projectId, Today(), cancellationToken).ConfigureAwait(false);
        return new BacklogResponse(project.Id, project.Key, project.Name, sprintLists, backlog, epics, can.Value);
    }

    /// <summary>The ranges the analytics accept; anything else falls back to 30 days.</summary>
    public static readonly IReadOnlyList<int> AnalyticsRanges = [7, 30, 90];

    public async Task<Result<DeliveryAnalyticsResponse>> AnalyticsAsync(int rangeDays, Guid? projectId, CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        if (projectId is { } id)
        {
            var project = await projects.GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (project is null)
            {
                return DeliveryAccess.ProjectNotFound;
            }

            var can = DeliveryAccess.For(viewer, project);
            if (can.IsFailure)
            {
                return can.Error!;
            }
        }

        var range = AnalyticsRanges.Contains(rangeDays) ? rangeDays : 30;
        return await readModel.AnalyticsAsync(viewer, projectId, range, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<TimelineResponse>> TimelineAsync(Guid projectId, CancellationToken cancellationToken)
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

        var (items, dependencies, unscheduled) = await readModel.TimelineAsync(projectId, Today(), cancellationToken).ConfigureAwait(false);
        var sprintList = await readModel.SprintsAsync(projectId, includeClosed: true, cancellationToken).ConfigureAwait(false);
        return new TimelineResponse(
            project.Id, project.Key, project.Name, project.StartDate, project.TargetDate, items, dependencies,
            sprintList.Where(sprint => sprint.StartDate is not null && sprint.EndDate is not null && sprint.State != "Cancelled").ToList(),
            unscheduled, can.Value);
    }

    public async Task<RoadmapResponse> RoadmapAsync(CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        return new RoadmapResponse(await readModel.RoadmapAsync(viewer, Today(), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The last closed sprints' committed and completed work, oldest first (for the velocity chart).</summary>
    public async Task<Result<IReadOnlyList<SprintVelocityResponse>>> VelocityAsync(Guid projectId, CancellationToken cancellationToken)
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

        var all = await readModel.SprintsAsync(projectId, includeClosed: true, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SprintVelocityResponse> velocity = all
            .Where(sprint => sprint.State == "Completed")
            .OrderByDescending(sprint => sprint.ClosedAtUtc)
            .Take(8)
            .OrderBy(sprint => sprint.ClosedAtUtc)
            .Select(sprint => new SprintVelocityResponse(
                sprint.Id, sprint.Name, sprint.CommittedPoints ?? 0, sprint.CompletedPoints ?? 0, sprint.CommittedIssueCount ?? 0,
                sprint.CompletedIssueCount ?? 0, sprint.ClosedAtUtc))
            .ToList();
        return Result.Success(velocity);
    }

    public async Task<DeliveryDashboardResponse> DashboardAsync(CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        return await readModel.DashboardAsync(viewer, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
