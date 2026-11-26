using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Application.Delivery;

/// <summary>
/// Read-side projections for the delivery workspace: one query per screen section, names resolved in SQL, filtered to the projects
/// the viewer may see. Implemented in Infrastructure; nothing here returns entities or IQueryable.
/// </summary>
public interface IDeliveryReadModel
{
    Task<IReadOnlyList<DeliveryProjectListItemResponse>> ListProjectsAsync(DeliveryViewer viewer, CancellationToken cancellationToken);

    Task<(int Total, int Done)> ProjectIssueCountsAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    Task<PagedResult<IssueListItemResponse>> SearchIssuesAsync(DeliveryViewer viewer, IssueQuery query, DateOnly today, CancellationToken cancellationToken);

    Task<IssueListItemResponse?> IssueAsync(Guid issueId, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<IssueListItemResponse>> ChildrenAsync(Guid issueId, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<IssueLinkResponse>> LinksAsync(Guid issueId, CancellationToken cancellationToken);

    Task<IReadOnlyList<IssueCommentResponse>> CommentsAsync(Guid issueId, Guid viewerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<IssueHistoryResponse>> HistoryAsync(Guid issueId, CancellationToken cancellationToken);

    Task<(int Count, bool IsWatching)> WatchersAsync(Guid issueId, Guid viewerId, CancellationToken cancellationToken);

    /// <summary>Issues of the project (or only of <paramref name="sprintId"/>), subtasks excluded, in rank order.</summary>
    Task<IReadOnlyList<IssueListItemResponse>> BoardIssuesAsync(Guid projectId, Guid? sprintId, DateOnly today, CancellationToken cancellationToken);

    /// <summary>Open sprints with their issues and the backlog (no sprint, not done), in rank order; epics separately.</summary>
    Task<(IReadOnlyList<SprintBacklogResponse> Sprints, IReadOnlyList<IssueListItemResponse> Backlog, IReadOnlyList<IssueListItemResponse> Epics)> BacklogAsync(
        Guid projectId, DateOnly today, CancellationToken cancellationToken);

    Task<SprintResponse?> SprintAsync(Guid sprintId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SprintResponse>> SprintsAsync(Guid projectId, bool includeClosed, CancellationToken cancellationToken);

    Task<DeliveryDashboardResponse> DashboardAsync(DeliveryViewer viewer, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Workspace analytics for the visible projects (or only <paramref name="projectId"/>) over the last <paramref name="rangeDays"/> days.</summary>
    Task<DeliveryAnalyticsResponse> AnalyticsAsync(DeliveryViewer viewer, Guid? projectId, int rangeDays, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Dated issues and epics of a project (subtasks excluded), "blocks" links between them and how many issues have no date.</summary>
    Task<(IReadOnlyList<TimelineItemResponse> Items, IReadOnlyList<TimelineDependencyResponse> Dependencies, int Unscheduled)> TimelineAsync(
        Guid projectId, DateOnly today, CancellationToken cancellationToken);

    /// <summary>Visible, non-archived projects with their dated epics.</summary>
    Task<IReadOnlyList<RoadmapProjectResponse>> RoadmapAsync(DeliveryViewer viewer, DateOnly today, CancellationToken cancellationToken);

    /// <summary>Every issue (subtasks excluded) that is or ever was in the sprint, its sprint/status/points history and the project's done statuses.</summary>
    Task<(IReadOnlyList<SprintIssueState> Issues, IReadOnlyList<SprintHistoryEvent> Events, IReadOnlySet<Guid> DoneStatusIds)> SprintHistoryAsync(
        Guid sprintId, Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<IssueAttachmentResponse>> AttachmentsAsync(Guid issueId, Guid viewerId, bool canManage, CancellationToken cancellationToken);

    Task<(IReadOnlyList<WorkLogResponse> Logs, int TotalMinutes)> WorkLogsAsync(Guid issueId, Guid viewerId, bool canManage, CancellationToken cancellationToken);

    Task<IReadOnlyList<SavedIssueFilterResponse>> SavedFiltersAsync(Guid ownerId, CancellationToken cancellationToken);
}
