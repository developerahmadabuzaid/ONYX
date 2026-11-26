using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.ApiClients;

/// <summary>The delivery workspace endpoints of Onyx.API. The API enforces every permission and project role.</summary>
public interface IAdminDeliveryApiClient
{
    Task<ApiResult<DeliveryDashboardResponse>> DashboardAsync(CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<DeliveryProjectListItemResponse>>> ListProjectsAsync(CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<DeliveryUserResponse>>> EligibleUsersAsync(CancellationToken cancellationToken);

    Task<ApiResult<DeliveryProjectResponse>> GetProjectAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<DeliveryProjectResponse>> CreateProjectAsync(SaveDeliveryProjectRequest request, CancellationToken cancellationToken);

    Task<ApiResult<DeliveryProjectResponse>> UpdateProjectAsync(Guid id, SaveDeliveryProjectRequest request, CancellationToken cancellationToken);

    Task<ApiResult<DeliveryProjectResponse>> SaveMemberAsync(Guid id, SaveProjectMemberRequest request, CancellationToken cancellationToken);

    Task<ApiResult<DeliveryProjectResponse>> RemoveMemberAsync(Guid id, Guid userId, uint version, CancellationToken cancellationToken);

    Task<ApiResult<DeliveryProjectResponse>> SetWipLimitAsync(Guid id, Guid statusId, SetWipLimitRequest request, CancellationToken cancellationToken);

    Task<ApiResult<BoardResponse>> BoardAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ApiResult<BacklogResponse>> BacklogAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<SprintResponse>>> SprintsAsync(Guid projectId, bool includeClosed, CancellationToken cancellationToken);

    Task<ApiResult<SprintResponse>> CreateSprintAsync(Guid projectId, SaveSprintRequest request, CancellationToken cancellationToken);

    Task<ApiResult<SprintResponse>> PlanSprintAsync(Guid sprintId, SaveSprintRequest request, CancellationToken cancellationToken);

    /// <summary><paramref name="action"/> is start, complete or cancel.</summary>
    Task<ApiResult<SprintResponse>> ChangeSprintAsync(Guid sprintId, string action, CloseSprintRequest request, CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<IssueListItemResponse>>> SearchIssuesAsync(IssueQuery query, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> GetIssueAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> CreateIssueAsync(SaveIssueRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> UpdateIssueAsync(Guid id, SaveIssueRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> TransitionIssueAsync(Guid id, TransitionIssueRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> MoveIssueAsync(Guid id, MoveIssueRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteIssueAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> AddCommentAsync(Guid id, SaveCommentRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> EditCommentAsync(Guid id, Guid commentId, SaveCommentRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> DeleteCommentAsync(Guid id, Guid commentId, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> AddLinkAsync(Guid id, CreateIssueLinkRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> RemoveLinkAsync(Guid id, Guid linkId, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> SetWatchingAsync(Guid id, bool watching, CancellationToken cancellationToken);

    Task<ApiResult<DeliveryAnalyticsResponse>> AnalyticsAsync(int rangeDays, Guid? projectId, CancellationToken cancellationToken);

    Task<ApiResult<RoadmapResponse>> RoadmapAsync(CancellationToken cancellationToken);

    Task<ApiResult<TimelineResponse>> TimelineAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<SprintVelocityResponse>>> VelocityAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ApiResult<SprintReportResponse>> SprintReportAsync(Guid sprintId, CancellationToken cancellationToken);

    Task<ApiResult<SprintReportResponse>> SaveRetrospectiveAsync(Guid sprintId, SaveRetrospectiveRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IssueDetailResponse>> RescheduleIssueAsync(Guid id, RescheduleIssueRequest request, CancellationToken cancellationToken);

    Task<ApiResult<BulkIssueResultResponse>> BulkAsync(BulkIssueRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IssueAttachmentResponse>> UploadAttachmentAsync(Guid id, Stream content, string fileName, string? contentType, CancellationToken cancellationToken);

    /// <summary>Streams an attachment; null when it does not exist or may not be seen.</summary>
    Task<ApiFileStream?> OpenAttachmentAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteAttachmentAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken);

    Task<ApiResult<WorkLogResponse>> AddWorkLogAsync(Guid id, SaveWorkLogRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteWorkLogAsync(Guid id, Guid workLogId, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<SavedIssueFilterResponse>>> SavedFiltersAsync(CancellationToken cancellationToken);

    Task<ApiResult<SavedIssueFilterResponse>> SaveFilterAsync(SaveIssueFilterRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteFilterAsync(Guid id, CancellationToken cancellationToken);
}
