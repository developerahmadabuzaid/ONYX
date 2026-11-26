namespace Onyx.Contracts.Delivery;

/// <summary>
/// Workspace analytics for the caller's visible projects (or one project): headline figures, a created-versus-completed timeline,
/// status and project-health breakdowns, per-person workload, recent activity and the dashboard lists. Every figure is counted
/// from the database; "completed" means currently in a Done status, dated by when it got there. Days are UTC.
/// </summary>
/// <param name="RangeDays">7, 30 or 90: the period the in-range figures and the timeline cover (ending today).</param>
/// <param name="Bucket">"day" or "week": the width of each timeline point.</param>
public sealed record DeliveryAnalyticsResponse(
    int RangeDays,
    DateOnly From,
    DateOnly To,
    string Bucket,
    DeliveryKpisResponse Kpis,
    IReadOnlyList<DeliveryTrendPointResponse> Timeline,
    StatusBreakdownResponse Status,
    IReadOnlyList<ProjectHealthResponse> Projects,
    IReadOnlyList<TeamMemberStatsResponse> Team,
    IReadOnlyList<ActivityResponse> RecentActivity,
    IReadOnlyList<IssueListItemResponse> MyOpenIssues,
    IReadOnlyList<IssueListItemResponse> UpcomingDeadlines,
    IReadOnlyList<IssueListItemResponse> RecentlyUpdated,
    IReadOnlyList<ActiveSprintSummaryResponse> ActiveSprints);
