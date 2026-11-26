using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Onyx.Application.Delivery;
using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;
using Onyx.Domain.Delivery;

namespace Onyx.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL projections for the delivery screens. Every list is one query with names joined in, filtered to the projects the viewer may
/// see, paginated or capped. Nothing here is tracked.
/// </summary>
internal sealed class DeliveryReadModel(ApplicationDbContext dbContext) : IDeliveryReadModel
{
    private const int DashboardListSize = 8;
    private const int ActivitySize = 12;
    private const int HistorySize = 200;

    public async Task<IReadOnlyList<DeliveryProjectListItemResponse>> ListProjectsAsync(DeliveryViewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var visible = VisibleProjectIds(viewer);
        var rows = await dbContext.DeliveryProjects.AsNoTracking()
            .Where(project => visible.Contains(project.Id))
            .Select(project => new
            {
                project.Id,
                project.Key,
                project.Name,
                project.Type,
                project.Classification,
                project.ClientName,
                project.Status,
                project.Priority,
                project.TargetDate,
                MyRole = dbContext.DeliveryProjectMembers.Where(member => member.ProjectId == project.Id && member.UserId == viewer.UserId).Select(member => (ProjectRole?)member.Role).FirstOrDefault(),

                // Epics group work and are not counted as tasks, the same rule as the workspace analytics.
                Total = dbContext.DeliveryIssues.Count(issue => issue.ProjectId == project.Id && issue.Type != IssueType.Epic),
                Done = dbContext.DeliveryIssues.Count(issue => issue.ProjectId == project.Id && issue.Type != IssueType.Epic
                    && dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category == StatusCategory.Done)),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .OrderBy(row => row.Status == DeliveryProjectStatus.Archived)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => new DeliveryProjectListItemResponse(
                row.Id, row.Key, row.Name, row.Type.ToString(), row.Classification.ToString(), row.ClientName, row.Status.ToString(), row.Priority.ToString(),
                row.TargetDate, row.MyRole?.ToString(), row.Total, row.Total - row.Done, row.Done))
            .ToList();
    }

    public async Task<(int Total, int Done)> ProjectIssueCountsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        // Epics group work and are not counted as tasks, the same rule as the workspace analytics.
        var total = await dbContext.DeliveryIssues.CountAsync(issue => issue.ProjectId == projectId && issue.Type != IssueType.Epic, cancellationToken).ConfigureAwait(false);
        var done = await dbContext.DeliveryIssues.CountAsync(
            issue => issue.ProjectId == projectId && issue.Type != IssueType.Epic
                && dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category == StatusCategory.Done),
            cancellationToken).ConfigureAwait(false);
        return (total, done);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = userIds.Distinct().ToList();
        return await dbContext.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PagedResult<IssueListItemResponse>> SearchIssuesAsync(DeliveryViewer viewer, IssueQuery query, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(query);
        var visible = VisibleProjectIds(viewer);
        var issues = dbContext.DeliveryIssues.AsNoTracking().Where(issue => visible.Contains(issue.ProjectId));

        if (query.ProjectId is { } projectId)
        {
            issues = issues.Where(issue => issue.ProjectId == projectId);
        }

        if (query.Mine)
        {
            issues = issues.Where(issue => issue.AssigneeId == viewer.UserId);
        }
        else if (query.AssigneeId is { } assigneeId)
        {
            issues = issues.Where(issue => issue.AssigneeId == assigneeId);
        }

        if (Enum.TryParse<StatusCategory>(query.StatusCategory, ignoreCase: true, out var category) && Enum.IsDefined(category))
        {
            issues = issues.Where(issue => dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category == category));
        }
        else if (string.Equals(query.StatusCategory, "Open", StringComparison.OrdinalIgnoreCase))
        {
            issues = issues.Where(issue => dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category != StatusCategory.Done));
        }

        if (Enum.TryParse<IssueType>(query.Type, ignoreCase: true, out var type) && Enum.IsDefined(type))
        {
            issues = issues.Where(issue => issue.Type == type);
        }

        if (Enum.TryParse<DeliveryPriority>(query.Priority, ignoreCase: true, out var priority) && Enum.IsDefined(priority))
        {
            issues = issues.Where(issue => issue.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(query.Label))
        {
            var label = query.Label.Trim().ToLowerInvariant();
            issues = issues.Where(issue => issue.Labels.Contains(label));
        }

        if (query.SprintId is { } sprintId)
        {
            issues = issues.Where(issue => issue.SprintId == sprintId);
        }

        if (query.EpicId is { } epicId)
        {
            issues = issues.Where(issue => issue.EpicId == epicId);
        }

        if (query.OverdueOnly)
        {
            issues = issues.Where(issue => issue.DueDate != null && issue.DueDate < today
                && dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category != StatusCategory.Done));
        }

        if (query.DueFrom is { } dueFrom)
        {
            issues = issues.Where(issue => issue.DueDate != null && issue.DueDate >= dueFrom);
        }

        if (query.DueTo is { } dueTo)
        {
            issues = issues.Where(issue => issue.DueDate != null && issue.DueDate <= dueTo);
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var pattern = "%" + Escape(query.Text) + "%";
            issues = issues.Where(issue => EF.Functions.ILike(issue.Title, pattern, "\\") || EF.Functions.ILike(issue.Key, pattern, "\\"));
        }

        var total = await issues.CountAsync(cancellationToken).ConfigureAwait(false);
        var ordered = (query.Sort ?? "updated").ToLowerInvariant() switch
        {
            "created" => issues.OrderByDescending(issue => issue.CreatedAtUtc),
            "priority" => issues.OrderBy(issue => issue.Priority == DeliveryPriority.Highest ? 0
                    : issue.Priority == DeliveryPriority.High ? 1
                    : issue.Priority == DeliveryPriority.Medium ? 2
                    : issue.Priority == DeliveryPriority.Low ? 3 : 4)
                .ThenByDescending(issue => issue.UpdatedAtUtc ?? issue.CreatedAtUtc),
            "due" => issues.OrderBy(issue => issue.DueDate == null).ThenBy(issue => issue.DueDate),
            "key" => issues.OrderBy(issue => issue.ProjectId).ThenBy(issue => issue.Number),
            _ => issues.OrderByDescending(issue => issue.UpdatedAtUtc ?? issue.CreatedAtUtc),
        };

        var page = await Project(ordered.ThenBy(issue => issue.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize), today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedResult<IssueListItemResponse>(page, query.Page, query.PageSize, total);
    }

    public Task<IssueListItemResponse?> IssueAsync(Guid issueId, DateOnly today, CancellationToken cancellationToken) =>
        Project(dbContext.DeliveryIssues.AsNoTracking().Where(issue => issue.Id == issueId), today).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<IssueListItemResponse>> ChildrenAsync(Guid issueId, DateOnly today, CancellationToken cancellationToken) =>
        await Project(
                dbContext.DeliveryIssues.AsNoTracking().Where(issue => issue.ParentId == issueId || issue.EpicId == issueId).OrderBy(issue => issue.Rank),
                today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<IssueLinkResponse>> LinksAsync(Guid issueId, CancellationToken cancellationToken)
    {
        var outward = from link in dbContext.DeliveryIssueLinks.AsNoTracking()
                      where link.SourceIssueId == issueId
                      join other in dbContext.DeliveryIssues on link.TargetIssueId equals other.Id
                      join status in dbContext.DeliveryWorkflowStatuses on other.StatusId equals status.Id
                      select new { LinkId = link.Id, link.Type, Direction = "outward", OtherId = other.Id, other.Key, other.Title, status.Category, link.CreatedAtUtc };
        var inward = from link in dbContext.DeliveryIssueLinks.AsNoTracking()
                     where link.TargetIssueId == issueId
                     join other in dbContext.DeliveryIssues on link.SourceIssueId equals other.Id
                     join status in dbContext.DeliveryWorkflowStatuses on other.StatusId equals status.Id
                     select new { LinkId = link.Id, link.Type, Direction = "inward", OtherId = other.Id, other.Key, other.Title, status.Category, link.CreatedAtUtc };

        var outRows = await outward.ToListAsync(cancellationToken).ConfigureAwait(false);
        var inRows = await inward.ToListAsync(cancellationToken).ConfigureAwait(false);
        return outRows.Select(row => (Row: new IssueLinkResponse(row.LinkId, row.Type.ToString(), row.Direction, row.OtherId, row.Key, row.Title, row.Category.ToString()), row.CreatedAtUtc))
            .Concat(inRows.Select(row => (Row: new IssueLinkResponse(row.LinkId, row.Type.ToString(), row.Direction, row.OtherId, row.Key, row.Title, row.Category.ToString()), row.CreatedAtUtc)))
            .OrderBy(pair => pair.CreatedAtUtc)
            .Select(pair => pair.Row)
            .ToList();
    }

    public async Task<IReadOnlyList<IssueCommentResponse>> CommentsAsync(Guid issueId, Guid viewerId, CancellationToken cancellationToken) =>
        await (from comment in dbContext.DeliveryIssueComments.AsNoTracking()
               where comment.IssueId == issueId
               join author in dbContext.Users on comment.AuthorId equals author.Id
               orderby comment.CreatedAtUtc
               select new IssueCommentResponse(
                   comment.Id, comment.AuthorId, author.DisplayName, comment.ParentCommentId, comment.Body, comment.CreatedAtUtc, comment.EditedAtUtc,
                   comment.IsDeleted, !comment.IsDeleted && comment.AuthorId == viewerId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<IssueHistoryResponse>> HistoryAsync(Guid issueId, CancellationToken cancellationToken)
    {
        var rows = await (from entry in dbContext.DeliveryIssueHistory.AsNoTracking()
                          where entry.IssueId == issueId
                          orderby entry.AtUtc descending
                          select new
                          {
                              entry.Field,
                              entry.OldValue,
                              entry.NewValue,
                              entry.AtUtc,
                              Actor = dbContext.Users.Where(user => user.Id == entry.ActorId).Select(user => user.DisplayName).FirstOrDefault(),
                          })
            .Take(HistorySize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Ids recorded for relational fields are shown as names, keys or statuses.
        var ids = rows.Where(row => IsReferenceField(row.Field))
            .SelectMany(row => new[] { row.OldValue, row.NewValue })
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var labels = new Dictionary<Guid, string>();
        if (ids.Count > 0)
        {
            foreach (var row in await dbContext.DeliveryWorkflowStatuses.AsNoTracking().Where(status => ids.Contains(status.Id)).Select(status => new { status.Id, status.Name }).ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                labels[row.Id] = row.Name;
            }

            foreach (var row in await dbContext.Users.AsNoTracking().Where(user => ids.Contains(user.Id)).Select(user => new { user.Id, user.DisplayName }).ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                labels[row.Id] = row.DisplayName;
            }

            foreach (var row in await dbContext.DeliverySprints.AsNoTracking().Where(sprint => ids.Contains(sprint.Id)).Select(sprint => new { sprint.Id, sprint.Name }).ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                labels[row.Id] = row.Name;
            }

            foreach (var row in await dbContext.DeliveryIssues.AsNoTracking().Where(issue => ids.Contains(issue.Id)).Select(issue => new { issue.Id, issue.Key }).ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                labels[row.Id] = row.Key;
            }
        }

        string? Display(string field, string? value) =>
            IsReferenceField(field) && Guid.TryParse(value, out var id) ? labels.GetValueOrDefault(id, value) : value;

        return rows.Select(row => new IssueHistoryResponse(row.Field, Display(row.Field, row.OldValue), Display(row.Field, row.NewValue), row.Actor, row.AtUtc)).ToList();
    }

    public async Task<(int Count, bool IsWatching)> WatchersAsync(Guid issueId, Guid viewerId, CancellationToken cancellationToken)
    {
        var watchers = await dbContext.DeliveryIssueWatchers.AsNoTracking().Where(watcher => watcher.IssueId == issueId).Select(watcher => watcher.UserId).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (watchers.Count, watchers.Contains(viewerId));
    }

    public async Task<IReadOnlyList<IssueListItemResponse>> BoardIssuesAsync(Guid projectId, Guid? sprintId, DateOnly today, CancellationToken cancellationToken) =>
        await Project(
                dbContext.DeliveryIssues.AsNoTracking()
                    .Where(issue => issue.ProjectId == projectId && issue.Type != IssueType.Subtask && issue.Type != IssueType.Epic)
                    .Where(issue => sprintId == null || issue.SprintId == sprintId)
                    .OrderBy(issue => issue.Rank),
                today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<SprintBacklogResponse> Sprints, IReadOnlyList<IssueListItemResponse> Backlog, IReadOnlyList<IssueListItemResponse> Epics)> BacklogAsync(
        Guid projectId, DateOnly today, CancellationToken cancellationToken)
    {
        var openSprints = await Sprints(dbContext.DeliverySprints.AsNoTracking()
                .Where(sprint => sprint.ProjectId == projectId && (sprint.State == SprintState.Planned || sprint.State == SprintState.Active)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var sprintIds = openSprints.Select(sprint => sprint.Id).ToList();
        var scheduled = await Project(
                dbContext.DeliveryIssues.AsNoTracking()
                    .Where(issue => issue.ProjectId == projectId && issue.SprintId != null && sprintIds.Contains(issue.SprintId.Value) && issue.Type != IssueType.Subtask)
                    .OrderBy(issue => issue.Rank),
                today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var backlog = await Project(
                dbContext.DeliveryIssues.AsNoTracking()
                    .Where(issue => issue.ProjectId == projectId && issue.SprintId == null && issue.Type != IssueType.Subtask && issue.Type != IssueType.Epic)
                    .Where(issue => dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category != StatusCategory.Done))
                    .OrderBy(issue => issue.Rank),
                today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var epics = await Project(
                dbContext.DeliveryIssues.AsNoTracking().Where(issue => issue.ProjectId == projectId && issue.Type == IssueType.Epic).OrderBy(issue => issue.Rank),
                today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lists = openSprints
            .OrderBy(sprint => sprint.State == nameof(SprintState.Active) ? 0 : 1)
            .ThenBy(sprint => sprint.StartDate ?? DateOnly.MaxValue)
            .Select(sprint => new SprintBacklogResponse(sprint, scheduled.Where(issue => issue.SprintId == sprint.Id).ToList()))
            .ToList();
        return (lists, backlog, epics);
    }

    public Task<SprintResponse?> SprintAsync(Guid sprintId, CancellationToken cancellationToken) =>
        Sprints(dbContext.DeliverySprints.AsNoTracking().Where(sprint => sprint.Id == sprintId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SprintResponse>> SprintsAsync(Guid projectId, bool includeClosed, CancellationToken cancellationToken) =>
        await Sprints(dbContext.DeliverySprints.AsNoTracking()
                .Where(sprint => sprint.ProjectId == projectId)
                .Where(sprint => includeClosed || sprint.State == SprintState.Planned || sprint.State == SprintState.Active)
                .OrderByDescending(sprint => sprint.StartDate)
                .ThenByDescending(sprint => sprint.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<DeliveryDashboardResponse> DashboardAsync(DeliveryViewer viewer, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var weekStart = now.UtcDateTime.Date.AddDays(-(((int)now.UtcDateTime.DayOfWeek + 6) % 7));
        var weekStartUtc = new DateTimeOffset(weekStart, TimeSpan.Zero);
        var visible = VisibleProjectIds(viewer);
        var visibleIssues = dbContext.DeliveryIssues.AsNoTracking().Where(issue => visible.Contains(issue.ProjectId));
        var open = visibleIssues.Where(issue => dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category != StatusCategory.Done));

        var activeProjects = await dbContext.DeliveryProjects.AsNoTracking().CountAsync(project => visible.Contains(project.Id) && project.Status == DeliveryProjectStatus.Active, cancellationToken).ConfigureAwait(false);
        var openIssues = await open.CountAsync(cancellationToken).ConfigureAwait(false);
        var assignedToMe = await open.CountAsync(issue => issue.AssigneeId == viewer.UserId, cancellationToken).ConfigureAwait(false);
        var overdue = await open.CountAsync(issue => issue.DueDate != null && issue.DueDate < today, cancellationToken).ConfigureAwait(false);
        var completedThisWeek = await visibleIssues.CountAsync(
            issue => issue.ResolvedAtUtc != null && issue.ResolvedAtUtc >= weekStartUtc
                && dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category == StatusCategory.Done),
            cancellationToken).ConfigureAwait(false);

        var activeSprintRows = await (from sprint in dbContext.DeliverySprints.AsNoTracking()
                                      where visible.Contains(sprint.ProjectId) && sprint.State == SprintState.Active
                                      join project in dbContext.DeliveryProjects on sprint.ProjectId equals project.Id
                                      select new { sprint.Id, sprint.ProjectId, project.Key })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var activeSprintIds = activeSprintRows.Select(row => row.Id).ToList();
        var sprintResponses = await Sprints(dbContext.DeliverySprints.AsNoTracking().Where(sprint => activeSprintIds.Contains(sprint.Id)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var activeSprints = activeSprintRows
            .Join(sprintResponses, row => row.Id, sprint => sprint.Id, (row, sprint) => new ActiveSprintSummaryResponse(row.ProjectId, row.Key, sprint))
            .ToList();

        var horizon = today.AddDays(14);
        var upcoming = await Project(open.Where(issue => issue.DueDate != null && issue.DueDate >= today && issue.DueDate <= horizon).OrderBy(issue => issue.DueDate).Take(DashboardListSize), today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var recent = await Project(visibleIssues.OrderByDescending(issue => issue.UpdatedAtUtc ?? issue.CreatedAtUtc).Take(DashboardListSize), today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var progress = await dbContext.DeliveryProjects.AsNoTracking()
            .Where(project => visible.Contains(project.Id) && project.Status != DeliveryProjectStatus.Archived && project.Status != DeliveryProjectStatus.Completed)
            .OrderBy(project => project.TargetDate == null)
            .ThenBy(project => project.TargetDate)
            .Take(DashboardListSize)
            .Select(project => new ProjectProgressResponse(
                project.Id,
                project.Key,
                project.Name,
                dbContext.DeliveryIssues.Count(issue => issue.ProjectId == project.Id),
                dbContext.DeliveryIssues.Count(issue => issue.ProjectId == project.Id
                    && dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category == StatusCategory.Done)),
                project.TargetDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activity = await (from entry in dbContext.DeliveryIssueHistory.AsNoTracking()
                              where visible.Contains(entry.ProjectId)
                              join issue in dbContext.DeliveryIssues on entry.IssueId equals issue.Id
                              orderby entry.AtUtc descending
                              select new ActivityResponse(
                                  issue.Id,
                                  issue.Key,
                                  issue.Title,
                                  entry.Field,
                                  dbContext.Users.Where(user => user.Id == entry.ActorId).Select(user => user.DisplayName).FirstOrDefault(),
                                  entry.AtUtc))
            .Take(ActivitySize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var workload = await (from issue in open
                              where issue.AssigneeId != null
                              group issue by issue.AssigneeId into byAssignee
                              select new { UserId = byAssignee.Key!.Value, Count = byAssignee.Count(), Points = byAssignee.Sum(issue => issue.StoryPoints ?? 0) })
            .OrderByDescending(row => row.Count)
            .Take(DashboardListSize * 2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var names = await DisplayNamesAsync(workload.Select(row => row.UserId).ToList(), cancellationToken).ConfigureAwait(false);

        return new DeliveryDashboardResponse(
            activeProjects,
            openIssues,
            assignedToMe,
            overdue,
            completedThisWeek,
            activeSprints,
            upcoming,
            progress,
            recent,
            activity,
            workload.Select(row => new WorkloadResponse(row.UserId, names.GetValueOrDefault(row.UserId, string.Empty), row.Count, row.Points)).ToList());
    }

    public async Task<DeliveryAnalyticsResponse> AnalyticsAsync(
        DeliveryViewer viewer, Guid? projectId, int rangeDays, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var from = today.AddDays(-(rangeDays - 1));
        var previousFrom = from.AddDays(-rangeDays);
        var fromUtc = StartOf(from);
        var previousFromUtc = StartOf(previousFrom);
        var visible = VisibleProjectIds(viewer);
        var projectScope = dbContext.DeliveryProjects.AsNoTracking().Where(project => visible.Contains(project.Id));
        if (projectId is { } onlyProject)
        {
            projectScope = projectScope.Where(project => project.Id == onlyProject);
        }

        var scopeIds = projectScope.Select(project => project.Id);

        // Epics group work; they are not counted as tasks.
        var tasks = dbContext.DeliveryIssues.AsNoTracking().Where(issue => scopeIds.Contains(issue.ProjectId) && issue.Type != IssueType.Epic);
        var withCategory = from issue in tasks
                           join status in dbContext.DeliveryWorkflowStatuses on issue.StatusId equals status.Id
                           select new { Issue = issue, status.Category };
        var open = withCategory.Where(row => row.Category != StatusCategory.Done).Select(row => row.Issue);

        var projectRows = await projectScope
            .Select(project => new
            {
                project.Id,
                project.Key,
                project.Name,
                project.Status,
                project.Priority,
                project.StartDate,
                project.TargetDate,
                project.CreatedAtUtc,
                Updated = project.UpdatedAtUtc ?? project.CreatedAtUtc,
                LastIssueUpdate = dbContext.DeliveryIssues.Where(issue => issue.ProjectId == project.Id).Max(issue => (DateTimeOffset?)(issue.UpdatedAtUtc ?? issue.CreatedAtUtc)),
                Members = dbContext.DeliveryProjectMembers.Count(member => member.ProjectId == project.Id),
                Total = dbContext.DeliveryIssues.Count(issue => issue.ProjectId == project.Id && issue.Type != IssueType.Epic),
                Done = dbContext.DeliveryIssues.Count(issue => issue.ProjectId == project.Id && issue.Type != IssueType.Epic
                    && dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category == StatusCategory.Done)),
                Overdue = dbContext.DeliveryIssues.Count(issue => issue.ProjectId == project.Id && issue.Type != IssueType.Epic && issue.DueDate != null && issue.DueDate < today
                    && dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category != StatusCategory.Done)),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var categoryRows = await withCategory
            .GroupBy(row => new { row.Category, Overdue = row.Issue.DueDate != null && row.Issue.DueDate < today })
            .Select(group => new { group.Key.Category, group.Key.Overdue, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        int Count(StatusCategory category, bool? overdue = null) =>
            categoryRows.Where(row => row.Category == category && (overdue is null || row.Overdue == overdue)).Sum(row => row.Count);

        var createdTimes = await tasks.Where(issue => issue.CreatedAtUtc >= previousFromUtc).Select(issue => issue.CreatedAtUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var completedTimes = await withCategory
            .Where(row => row.Category == StatusCategory.Done && row.Issue.ResolvedAtUtc != null && row.Issue.ResolvedAtUtc >= previousFromUtc)
            .Select(row => row.Issue.ResolvedAtUtc!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var assignedToMe = await open.CountAsync(issue => issue.AssigneeId == viewer.UserId, cancellationToken).ConfigureAwait(false);

        var kpis = new DeliveryKpisResponse(
            projectRows.Count(row => row.Status != DeliveryProjectStatus.Archived),
            projectRows.Count(row => row.CreatedAtUtc >= fromUtc),
            projectRows.Count(row => row.Status == DeliveryProjectStatus.Active),
            categoryRows.Sum(row => row.Count),
            createdTimes.Count(time => time >= fromUtc),
            createdTimes.Count(time => time < fromUtc),
            Count(StatusCategory.Done),
            completedTimes.Count(time => time >= fromUtc),
            completedTimes.Count(time => time < fromUtc),
            Count(StatusCategory.ToDo),
            Count(StatusCategory.InProgress),
            Count(StatusCategory.ToDo, overdue: true) + Count(StatusCategory.InProgress, overdue: true),
            assignedToMe);

        // Daily points for 7 and 30 days; weekly points for 90 days (the last one may be shorter, ending today).
        var bucketDays = rangeDays > 31 ? 7 : 1;
        var timeline = new List<DeliveryTrendPointResponse>();
        for (var start = from; start <= today; start = start.AddDays(bucketDays))
        {
            var end = start.AddDays(bucketDays - 1);
            if (end > today)
            {
                end = today;
            }

            var (startUtc, endUtc) = (StartOf(start), StartOf(end.AddDays(1)));
            timeline.Add(new DeliveryTrendPointResponse(
                start,
                end,
                createdTimes.Count(time => time >= startUtc && time < endUtc),
                completedTimes.Count(time => time >= startUtc && time < endUtc)));
        }

        var breakdown = new StatusBreakdownResponse(
            Count(StatusCategory.Done),
            Count(StatusCategory.InProgress, overdue: false),
            Count(StatusCategory.ToDo, overdue: false),
            Count(StatusCategory.ToDo, overdue: true) + Count(StatusCategory.InProgress, overdue: true));

        var projectList = projectRows
            .Where(row => row.Status != DeliveryProjectStatus.Archived)
            .Select(row => new ProjectHealthResponse(
                row.Id, row.Key, row.Name, row.Status.ToString(), ProjectHealth.Of(row.Status, row.TargetDate, row.Overdue, today), row.Priority.ToString(),
                row.Total, row.Done, row.Overdue, row.Members, row.StartDate, row.TargetDate,
                row.LastIssueUpdate is { } issueUpdate && issueUpdate > row.Updated ? issueUpdate : row.Updated))
            .OrderByDescending(project => project.UpdatedAtUtc)
            .ToList();

        var teamRows = await withCategory
            .Where(row => row.Issue.AssigneeId != null && row.Category != StatusCategory.Done)
            .GroupBy(row => new { row.Issue.AssigneeId, row.Category, Overdue = row.Issue.DueDate != null && row.Issue.DueDate < today })
            .Select(group => new { group.Key.AssigneeId, group.Key.Category, group.Key.Overdue, Count = group.Count(), Points = group.Sum(row => row.Issue.StoryPoints ?? 0) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var completedByPerson = await withCategory
            .Where(row => row.Issue.AssigneeId != null && row.Category == StatusCategory.Done && row.Issue.ResolvedAtUtc != null && row.Issue.ResolvedAtUtc >= fromUtc)
            .GroupBy(row => row.Issue.AssigneeId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var people = teamRows.Select(row => row.AssigneeId!.Value).Concat(completedByPerson.Select(row => row.UserId!.Value)).Distinct().ToList();
        var names = await DisplayNamesAsync(people, cancellationToken).ConfigureAwait(false);
        var team = people
            .Select(userId =>
            {
                var mine = teamRows.Where(row => row.AssigneeId == userId).ToList();
                return new TeamMemberStatsResponse(
                    userId,
                    names.GetValueOrDefault(userId, string.Empty),
                    mine.Where(row => row.Category == StatusCategory.ToDo && !row.Overdue).Sum(row => row.Count),
                    mine.Where(row => row.Category == StatusCategory.InProgress && !row.Overdue).Sum(row => row.Count),
                    mine.Where(row => row.Overdue).Sum(row => row.Count),
                    mine.Sum(row => row.Points),
                    completedByPerson.Where(row => row.UserId == userId).Sum(row => row.Count));
            })
            .OrderByDescending(member => member.ToDo + member.InProgress + member.Overdue)
            .ThenByDescending(member => member.CompletedInRange)
            .ThenBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(12)
            .ToList();

        var activity = await (from entry in dbContext.DeliveryIssueHistory.AsNoTracking()
                              where scopeIds.Contains(entry.ProjectId)
                              join issue in dbContext.DeliveryIssues on entry.IssueId equals issue.Id
                              orderby entry.AtUtc descending
                              select new ActivityResponse(
                                  issue.Id,
                                  issue.Key,
                                  issue.Title,
                                  entry.Field,
                                  dbContext.Users.Where(user => user.Id == entry.ActorId).Select(user => user.DisplayName).FirstOrDefault(),
                                  entry.AtUtc))
            .Take(ActivitySize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var mineOpen = await Project(
                open.Where(issue => issue.AssigneeId == viewer.UserId)
                    .OrderBy(issue => issue.DueDate == null)
                    .ThenBy(issue => issue.DueDate)
                    .ThenByDescending(issue => issue.UpdatedAtUtc ?? issue.CreatedAtUtc)
                    .Take(DashboardListSize),
                today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var horizon = today.AddDays(14);
        var upcoming = await Project(
                open.Where(issue => issue.DueDate != null && issue.DueDate >= today && issue.DueDate <= horizon).OrderBy(issue => issue.DueDate).Take(DashboardListSize),
                today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var recent = await Project(tasks.OrderByDescending(issue => issue.UpdatedAtUtc ?? issue.CreatedAtUtc).Take(DashboardListSize), today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeSprintRows = await (from sprint in dbContext.DeliverySprints.AsNoTracking()
                                      where scopeIds.Contains(sprint.ProjectId) && sprint.State == SprintState.Active
                                      join project in dbContext.DeliveryProjects on sprint.ProjectId equals project.Id
                                      select new { sprint.Id, sprint.ProjectId, project.Key })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var activeSprintIds = activeSprintRows.Select(row => row.Id).ToList();
        var sprintResponses = await Sprints(dbContext.DeliverySprints.AsNoTracking().Where(sprint => activeSprintIds.Contains(sprint.Id)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var activeSprints = activeSprintRows
            .Join(sprintResponses, row => row.Id, sprint => sprint.Id, (row, sprint) => new ActiveSprintSummaryResponse(row.ProjectId, row.Key, sprint))
            .ToList();

        return new DeliveryAnalyticsResponse(
            rangeDays, from, today, bucketDays == 1 ? "day" : "week", kpis, timeline, breakdown, projectList, team, activity, mineOpen, upcoming, recent, activeSprints);
    }

    public async Task<(IReadOnlyList<TimelineItemResponse> Items, IReadOnlyList<TimelineDependencyResponse> Dependencies, int Unscheduled)> TimelineAsync(
        Guid projectId, DateOnly today, CancellationToken cancellationToken)
    {
        var rows = await TimelineRows(dbContext.DeliveryIssues.AsNoTracking().Where(issue => issue.ProjectId == projectId && issue.Type != IssueType.Subtask), today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var dated = rows.Where(row => row.StartDate is not null || row.DueDate is not null)
            .OrderBy(row => row.StartDate ?? row.DueDate)
            .ThenBy(row => row.Key, StringComparer.Ordinal)
            .ToList();
        var unscheduled = rows.Count(row => row.StartDate is null && row.DueDate is null && row.StatusCategory != nameof(StatusCategory.Done));
        var datedIds = dated.Select(row => row.Id).ToList();
        var dependencies = await dbContext.DeliveryIssueLinks.AsNoTracking()
            .Where(link => link.Type == IssueLinkType.Blocks && datedIds.Contains(link.SourceIssueId) && datedIds.Contains(link.TargetIssueId))
            .Select(link => new TimelineDependencyResponse(link.SourceIssueId, link.TargetIssueId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (dated, dependencies, unscheduled);
    }

    public async Task<IReadOnlyList<RoadmapProjectResponse>> RoadmapAsync(DeliveryViewer viewer, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var visible = VisibleProjectIds(viewer);
        var projectRows = await dbContext.DeliveryProjects.AsNoTracking()
            .Where(project => visible.Contains(project.Id) && project.Status != DeliveryProjectStatus.Archived)
            .Select(project => new
            {
                project.Id,
                project.Key,
                project.Name,
                project.Status,
                project.StartDate,
                project.TargetDate,
                Total = dbContext.DeliveryIssues.Count(issue => issue.ProjectId == project.Id && issue.Type != IssueType.Epic),
                Done = dbContext.DeliveryIssues.Count(issue => issue.ProjectId == project.Id && issue.Type != IssueType.Epic
                    && dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category == StatusCategory.Done)),
                Overdue = dbContext.DeliveryIssues.Count(issue => issue.ProjectId == project.Id && issue.Type != IssueType.Epic && issue.DueDate != null && issue.DueDate < today
                    && dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category != StatusCategory.Done)),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var ids = projectRows.Select(row => row.Id).ToList();
        var epics = await TimelineRows(
                dbContext.DeliveryIssues.AsNoTracking().Where(issue => ids.Contains(issue.ProjectId) && issue.Type == IssueType.Epic && (issue.StartDate != null || issue.DueDate != null)),
                today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var epicProjects = await dbContext.DeliveryIssues.AsNoTracking()
            .Where(issue => ids.Contains(issue.ProjectId) && issue.Type == IssueType.Epic)
            .Select(issue => new { issue.Id, issue.ProjectId })
            .ToDictionaryAsync(row => row.Id, row => row.ProjectId, cancellationToken)
            .ConfigureAwait(false);

        return projectRows
            .OrderBy(row => row.StartDate ?? row.TargetDate ?? DateOnly.MaxValue)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => new RoadmapProjectResponse(
                row.Id, row.Key, row.Name, row.Status.ToString(), ProjectHealth.Of(row.Status, row.TargetDate, row.Overdue, today),
                row.StartDate, row.TargetDate, row.Total, row.Done,
                epics.Where(epic => epicProjects.GetValueOrDefault(epic.Id) == row.Id).OrderBy(epic => epic.StartDate ?? epic.DueDate).ToList()))
            .ToList();
    }

    public async Task<(IReadOnlyList<SprintIssueState> Issues, IReadOnlyList<SprintHistoryEvent> Events, IReadOnlySet<Guid> DoneStatusIds)> SprintHistoryAsync(
        Guid sprintId, Guid projectId, CancellationToken cancellationToken)
    {
        var sprintKey = sprintId.ToString();
        var everInSprint = dbContext.DeliveryIssueHistory
            .Where(entry => entry.ProjectId == projectId && entry.Field == BurndownCalculator.SprintField && (entry.OldValue == sprintKey || entry.NewValue == sprintKey))
            .Select(entry => entry.IssueId);
        var states = await dbContext.DeliveryIssues.AsNoTracking()
            .Where(issue => issue.ProjectId == projectId && issue.Type != IssueType.Subtask && (issue.SprintId == sprintId || everInSprint.Contains(issue.Id)))
            .Select(issue => new SprintIssueState(issue.Id, issue.SprintId, issue.StatusId, issue.StoryPoints, issue.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var ids = states.Select(state => state.IssueId).ToList();
        var events = await dbContext.DeliveryIssueHistory.AsNoTracking()
            .Where(entry => ids.Contains(entry.IssueId)
                && (entry.Field == BurndownCalculator.SprintField || entry.Field == BurndownCalculator.StatusField || entry.Field == BurndownCalculator.PointsField))
            .OrderBy(entry => entry.AtUtc)
            .Select(entry => new SprintHistoryEvent(entry.IssueId, entry.Field, entry.OldValue, entry.NewValue, entry.AtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var done = await dbContext.DeliveryWorkflowStatuses.AsNoTracking()
            .Where(status => status.ProjectId == projectId && status.Category == StatusCategory.Done)
            .Select(status => status.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (states, events, done.ToHashSet());
    }

    public async Task<IReadOnlyList<IssueAttachmentResponse>> AttachmentsAsync(Guid issueId, Guid viewerId, bool canManage, CancellationToken cancellationToken) =>
        await (from attachment in dbContext.DeliveryIssueAttachments.AsNoTracking()
               where attachment.IssueId == issueId
               join uploader in dbContext.Users on attachment.UploadedById equals uploader.Id
               orderby attachment.UploadedAtUtc
               select new IssueAttachmentResponse(
                   attachment.Id,
                   attachment.FileName,
                   attachment.ContentType,
                   attachment.SizeBytes,
                   attachment.ContentType.StartsWith("image/"),
                   uploader.DisplayName,
                   attachment.UploadedAtUtc,
                   canManage || attachment.UploadedById == viewerId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<WorkLogResponse> Logs, int TotalMinutes)> WorkLogsAsync(Guid issueId, Guid viewerId, bool canManage, CancellationToken cancellationToken)
    {
        var logs = await (from log in dbContext.DeliveryIssueWorkLogs.AsNoTracking()
                          where log.IssueId == issueId
                          join person in dbContext.Users on log.UserId equals person.Id
                          orderby log.WorkDate descending, log.CreatedAtUtc descending
                          select new WorkLogResponse(log.Id, log.UserId, person.DisplayName, log.Minutes, log.WorkDate, log.Note, log.CreatedAtUtc, canManage || log.UserId == viewerId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (logs, logs.Sum(log => log.Minutes));
    }

    public async Task<IReadOnlyList<SavedIssueFilterResponse>> SavedFiltersAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var filters = await dbContext.DeliverySavedIssueFilters.AsNoTracking()
            .Where(filter => filter.OwnerId == ownerId)
            .Select(filter => new SavedIssueFilterResponse(filter.Id, filter.Name, filter.Query, filter.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return filters.OrderBy(filter => filter.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static DateTimeOffset StartOf(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private IQueryable<TimelineItemResponse> TimelineRows(IQueryable<Issue> source, DateOnly today) =>
        from issue in source
        join status in dbContext.DeliveryWorkflowStatuses on issue.StatusId equals status.Id
        select new TimelineItemResponse(
            issue.Id,
            issue.Key,
            issue.Title,
            issue.Type.ToString(),
            status.Name,
            status.Category.ToString(),
            issue.Priority.ToString(),
            dbContext.Users.Where(user => user.Id == issue.AssigneeId).Select(user => user.DisplayName).FirstOrDefault(),
            issue.StartDate,
            issue.DueDate,
            issue.EpicId,
            issue.Type == IssueType.Epic ? dbContext.DeliveryIssues.Count(child => child.EpicId == issue.Id) : 0,
            issue.Type == IssueType.Epic
                ? dbContext.DeliveryIssues.Count(child => child.EpicId == issue.Id
                    && dbContext.DeliveryWorkflowStatuses.Any(childStatus => childStatus.Id == child.StatusId && childStatus.Category == StatusCategory.Done))
                : 0,
            issue.DueDate != null && issue.DueDate < today && status.Category != StatusCategory.Done,
            issue.Version);

    private static bool IsReferenceField(string field) => field is "status" or "assignee" or "sprint" or "epic" or "parent";

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private IQueryable<Guid> VisibleProjectIds(DeliveryViewer viewer) =>
        viewer.IsAdministrator
            ? dbContext.DeliveryProjects.Select(project => project.Id)
            : dbContext.DeliveryProjectMembers.Where(member => member.UserId == viewer.UserId).Select(member => member.ProjectId);

    private IQueryable<IssueListItemResponse> Project(IQueryable<Issue> source, DateOnly today) =>
        from issue in source
        join status in dbContext.DeliveryWorkflowStatuses on issue.StatusId equals status.Id
        join project in dbContext.DeliveryProjects on issue.ProjectId equals project.Id
        select new IssueListItemResponse(
            issue.Id,
            issue.Key,
            issue.ProjectId,
            project.Key,
            issue.Type.ToString(),
            issue.Title,
            issue.StatusId,
            status.Name,
            status.SystemKey,
            status.Category.ToString(),
            issue.Priority.ToString(),
            issue.AssigneeId,
            dbContext.Users.Where(user => user.Id == issue.AssigneeId).Select(user => user.DisplayName).FirstOrDefault(),
            issue.StoryPoints,
            issue.DueDate,
            issue.Labels,
            issue.EpicId,
            dbContext.DeliveryIssues.Where(epic => epic.Id == issue.EpicId).Select(epic => epic.Key).FirstOrDefault(),
            dbContext.DeliveryIssues.Where(epic => epic.Id == issue.EpicId).Select(epic => epic.Title).FirstOrDefault(),
            issue.ParentId,
            issue.SprintId,
            issue.Rank,
            issue.DueDate != null && issue.DueDate < today && status.Category != StatusCategory.Done,
            issue.UpdatedAtUtc ?? issue.CreatedAtUtc,
            issue.Version,
            issue.StartDate);

    private IQueryable<SprintResponse> Sprints(IQueryable<Sprint> source) =>
        source.Select(sprint => new SprintResponse(
            sprint.Id,
            sprint.Name,
            sprint.Goal,
            sprint.StartDate,
            sprint.EndDate,
            sprint.State.ToString(),
            dbContext.DeliveryIssues.Count(issue => issue.SprintId == sprint.Id && issue.Type != IssueType.Subtask),
            dbContext.DeliveryIssues.Where(issue => issue.SprintId == sprint.Id && issue.Type != IssueType.Subtask).Sum(issue => issue.StoryPoints ?? 0),
            dbContext.DeliveryIssues.Count(issue => issue.SprintId == sprint.Id && issue.Type != IssueType.Subtask
                && dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category == StatusCategory.Done)),
            dbContext.DeliveryIssues.Where(issue => issue.SprintId == sprint.Id && issue.Type != IssueType.Subtask
                    && dbContext.DeliveryWorkflowStatuses.Any(status => status.Id == issue.StatusId && status.Category == StatusCategory.Done))
                .Sum(issue => issue.StoryPoints ?? 0),
            sprint.CommittedIssueCount,
            sprint.CommittedPoints,
            sprint.CompletedIssueCount,
            sprint.CompletedPoints,
            sprint.StartedAtUtc,
            sprint.ClosedAtUtc,
            sprint.Version));
}
