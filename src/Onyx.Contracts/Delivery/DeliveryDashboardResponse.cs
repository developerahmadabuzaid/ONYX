namespace Onyx.Contracts.Delivery;

/// <summary>Workspace dashboard: every figure is counted from the database for the projects the caller can see.</summary>
public sealed record DeliveryDashboardResponse(
    int ActiveProjects,
    int OpenIssues,
    int AssignedToMe,
    int Overdue,
    int CompletedThisWeek,
    IReadOnlyList<ActiveSprintSummaryResponse> ActiveSprints,
    IReadOnlyList<IssueListItemResponse> UpcomingDeadlines,
    IReadOnlyList<ProjectProgressResponse> ProjectProgress,
    IReadOnlyList<IssueListItemResponse> RecentlyUpdated,
    IReadOnlyList<ActivityResponse> RecentActivity,
    IReadOnlyList<WorkloadResponse> Workload);
