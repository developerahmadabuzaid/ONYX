using System.Globalization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.ApiClients;

public sealed class AdminDeliveryApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IAdminDeliveryApiClient
{
    private const string Root = "api/v1/admin/delivery";
    private const string Projects = Root + "/projects";
    private const string Issues = Root + "/issues";
    private const string Sprints = Root + "/sprints";

    private static readonly HashSet<string> SprintActions = new(StringComparer.Ordinal) { "start", "complete", "cancel" };

    public Task<ApiResult<DeliveryDashboardResponse>> DashboardAsync(CancellationToken cancellationToken) =>
        GetAsync<DeliveryDashboardResponse>($"{Root}/dashboard", cancellationToken);

    public Task<ApiResult<IReadOnlyList<DeliveryProjectListItemResponse>>> ListProjectsAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<DeliveryProjectListItemResponse>>(Projects, cancellationToken);

    public Task<ApiResult<IReadOnlyList<DeliveryUserResponse>>> EligibleUsersAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<DeliveryUserResponse>>($"{Projects}/users", cancellationToken);

    public Task<ApiResult<DeliveryProjectResponse>> GetProjectAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<DeliveryProjectResponse>($"{Projects}/{id:D}", cancellationToken);

    public Task<ApiResult<DeliveryProjectResponse>> CreateProjectAsync(SaveDeliveryProjectRequest request, CancellationToken cancellationToken) =>
        PostAsync<DeliveryProjectResponse>(Projects, request, cancellationToken);

    public Task<ApiResult<DeliveryProjectResponse>> UpdateProjectAsync(Guid id, SaveDeliveryProjectRequest request, CancellationToken cancellationToken) =>
        PutAsync<DeliveryProjectResponse>($"{Projects}/{id:D}", request, cancellationToken);

    public Task<ApiResult<DeliveryProjectResponse>> SaveMemberAsync(Guid id, SaveProjectMemberRequest request, CancellationToken cancellationToken) =>
        PostAsync<DeliveryProjectResponse>($"{Projects}/{id:D}/members", request, cancellationToken);

    public Task<ApiResult<DeliveryProjectResponse>> RemoveMemberAsync(Guid id, Guid userId, uint version, CancellationToken cancellationToken) =>
        DeleteAsync<DeliveryProjectResponse>($"{Projects}/{id:D}/members/{userId:D}?version={version.ToString(CultureInfo.InvariantCulture)}", cancellationToken);

    public Task<ApiResult<DeliveryProjectResponse>> SetWipLimitAsync(Guid id, Guid statusId, SetWipLimitRequest request, CancellationToken cancellationToken) =>
        PutAsync<DeliveryProjectResponse>($"{Projects}/{id:D}/statuses/{statusId:D}/wip", request, cancellationToken);

    public Task<ApiResult<BoardResponse>> BoardAsync(Guid projectId, CancellationToken cancellationToken) =>
        GetAsync<BoardResponse>($"{Projects}/{projectId:D}/board", cancellationToken);

    public Task<ApiResult<BacklogResponse>> BacklogAsync(Guid projectId, CancellationToken cancellationToken) =>
        GetAsync<BacklogResponse>($"{Projects}/{projectId:D}/backlog", cancellationToken);

    public Task<ApiResult<IReadOnlyList<SprintResponse>>> SprintsAsync(Guid projectId, bool includeClosed, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<SprintResponse>>($"{Projects}/{projectId:D}/sprints?includeClosed={(includeClosed ? "true" : "false")}", cancellationToken);

    public Task<ApiResult<SprintResponse>> CreateSprintAsync(Guid projectId, SaveSprintRequest request, CancellationToken cancellationToken) =>
        PostAsync<SprintResponse>($"{Projects}/{projectId:D}/sprints", request, cancellationToken);

    public Task<ApiResult<SprintResponse>> PlanSprintAsync(Guid sprintId, SaveSprintRequest request, CancellationToken cancellationToken) =>
        PutAsync<SprintResponse>($"{Sprints}/{sprintId:D}", request, cancellationToken);

    public Task<ApiResult<SprintResponse>> ChangeSprintAsync(Guid sprintId, string action, CloseSprintRequest request, CancellationToken cancellationToken) =>
        SprintActions.Contains(action)
            ? PostAsync<SprintResponse>($"{Sprints}/{sprintId:D}/{action}", request, cancellationToken)
            : throw new ArgumentOutOfRangeException(nameof(action));

    public Task<ApiResult<PagedResult<IssueListItemResponse>>> SearchIssuesAsync(IssueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parameters = new List<string>
        {
            "page=" + query.Page.ToString(CultureInfo.InvariantCulture),
            "pageSize=" + query.PageSize.ToString(CultureInfo.InvariantCulture),
        };
        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters.Add(name + "=" + Uri.EscapeDataString(value));
            }
        }

        Add("projectId", query.ProjectId?.ToString("D"));
        Add("mine", query.Mine ? "true" : null);
        Add("assigneeId", query.AssigneeId?.ToString("D"));
        Add("statusCategory", query.StatusCategory);
        Add("type", query.Type);
        Add("priority", query.Priority);
        Add("label", query.Label);
        Add("sprintId", query.SprintId?.ToString("D"));
        Add("epicId", query.EpicId?.ToString("D"));
        Add("text", query.Text);
        Add("overdueOnly", query.OverdueOnly ? "true" : null);
        Add("sort", query.Sort);
        Add("dueFrom", query.DueFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add("dueTo", query.DueTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return GetAsync<PagedResult<IssueListItemResponse>>($"{Issues}?{string.Join('&', parameters)}", cancellationToken);
    }

    public Task<ApiResult<IssueDetailResponse>> GetIssueAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<IssueDetailResponse>($"{Issues}/{id:D}", cancellationToken);

    public Task<ApiResult<IssueDetailResponse>> CreateIssueAsync(SaveIssueRequest request, CancellationToken cancellationToken) =>
        PostAsync<IssueDetailResponse>(Issues, request, cancellationToken);

    public Task<ApiResult<IssueDetailResponse>> UpdateIssueAsync(Guid id, SaveIssueRequest request, CancellationToken cancellationToken) =>
        PutAsync<IssueDetailResponse>($"{Issues}/{id:D}", request, cancellationToken);

    public Task<ApiResult<IssueDetailResponse>> TransitionIssueAsync(Guid id, TransitionIssueRequest request, CancellationToken cancellationToken) =>
        PostAsync<IssueDetailResponse>($"{Issues}/{id:D}/transition", request, cancellationToken);

    public Task<ApiResult<IssueDetailResponse>> MoveIssueAsync(Guid id, MoveIssueRequest request, CancellationToken cancellationToken) =>
        PostAsync<IssueDetailResponse>($"{Issues}/{id:D}/move", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteIssueAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"{Issues}/{id:D}", cancellationToken);

    public Task<ApiResult<IssueDetailResponse>> AddCommentAsync(Guid id, SaveCommentRequest request, CancellationToken cancellationToken) =>
        PostAsync<IssueDetailResponse>($"{Issues}/{id:D}/comments", request, cancellationToken);

    public Task<ApiResult<IssueDetailResponse>> EditCommentAsync(Guid id, Guid commentId, SaveCommentRequest request, CancellationToken cancellationToken) =>
        PutAsync<IssueDetailResponse>($"{Issues}/{id:D}/comments/{commentId:D}", request, cancellationToken);

    public Task<ApiResult<IssueDetailResponse>> DeleteCommentAsync(Guid id, Guid commentId, CancellationToken cancellationToken) =>
        DeleteAsync<IssueDetailResponse>($"{Issues}/{id:D}/comments/{commentId:D}", cancellationToken);

    public Task<ApiResult<IssueDetailResponse>> AddLinkAsync(Guid id, CreateIssueLinkRequest request, CancellationToken cancellationToken) =>
        PostAsync<IssueDetailResponse>($"{Issues}/{id:D}/links", request, cancellationToken);

    public Task<ApiResult<IssueDetailResponse>> RemoveLinkAsync(Guid id, Guid linkId, CancellationToken cancellationToken) =>
        DeleteAsync<IssueDetailResponse>($"{Issues}/{id:D}/links/{linkId:D}", cancellationToken);

    public Task<ApiResult<IssueDetailResponse>> SetWatchingAsync(Guid id, bool watching, CancellationToken cancellationToken) =>
        watching
            ? PutAsync<IssueDetailResponse>($"{Issues}/{id:D}/watch", new { }, cancellationToken)
            : DeleteAsync<IssueDetailResponse>($"{Issues}/{id:D}/watch", cancellationToken);

    public Task<ApiResult<DeliveryAnalyticsResponse>> AnalyticsAsync(int rangeDays, Guid? projectId, CancellationToken cancellationToken) =>
        GetAsync<DeliveryAnalyticsResponse>(
            $"{Root}/analytics?range={rangeDays.ToString(CultureInfo.InvariantCulture)}{(projectId is { } id ? $"&projectId={id:D}" : string.Empty)}", cancellationToken);

    public Task<ApiResult<RoadmapResponse>> RoadmapAsync(CancellationToken cancellationToken) =>
        GetAsync<RoadmapResponse>($"{Root}/roadmap", cancellationToken);

    public Task<ApiResult<TimelineResponse>> TimelineAsync(Guid projectId, CancellationToken cancellationToken) =>
        GetAsync<TimelineResponse>($"{Projects}/{projectId:D}/timeline", cancellationToken);

    public Task<ApiResult<IReadOnlyList<SprintVelocityResponse>>> VelocityAsync(Guid projectId, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<SprintVelocityResponse>>($"{Projects}/{projectId:D}/velocity", cancellationToken);

    public Task<ApiResult<SprintReportResponse>> SprintReportAsync(Guid sprintId, CancellationToken cancellationToken) =>
        GetAsync<SprintReportResponse>($"{Sprints}/{sprintId:D}/report", cancellationToken);

    public Task<ApiResult<SprintReportResponse>> SaveRetrospectiveAsync(Guid sprintId, SaveRetrospectiveRequest request, CancellationToken cancellationToken) =>
        PutAsync<SprintReportResponse>($"{Sprints}/{sprintId:D}/retrospective", request, cancellationToken);

    public Task<ApiResult<IssueDetailResponse>> RescheduleIssueAsync(Guid id, RescheduleIssueRequest request, CancellationToken cancellationToken) =>
        PostAsync<IssueDetailResponse>($"{Issues}/{id:D}/dates", request, cancellationToken);

    public Task<ApiResult<BulkIssueResultResponse>> BulkAsync(BulkIssueRequest request, CancellationToken cancellationToken) =>
        PostAsync<BulkIssueResultResponse>($"{Issues}/bulk", request, cancellationToken);

    public Task<ApiResult<IssueAttachmentResponse>> UploadAttachmentAsync(Guid id, Stream content, string fileName, string? contentType, CancellationToken cancellationToken) =>
        PostFileAsync<IssueAttachmentResponse>($"{Issues}/{id:D}/attachments", content, fileName, contentType, cancellationToken);

    public Task<ApiFileStream?> OpenAttachmentAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken) =>
        GetStreamAsync($"{Issues}/{id:D}/attachments/{attachmentId:D}", cancellationToken);

    public Task<ApiResult<NoContent>> DeleteAttachmentAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"{Issues}/{id:D}/attachments/{attachmentId:D}", cancellationToken);

    public Task<ApiResult<WorkLogResponse>> AddWorkLogAsync(Guid id, SaveWorkLogRequest request, CancellationToken cancellationToken) =>
        PostAsync<WorkLogResponse>($"{Issues}/{id:D}/worklogs", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteWorkLogAsync(Guid id, Guid workLogId, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"{Issues}/{id:D}/worklogs/{workLogId:D}", cancellationToken);

    public Task<ApiResult<IReadOnlyList<SavedIssueFilterResponse>>> SavedFiltersAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<SavedIssueFilterResponse>>($"{Root}/filters", cancellationToken);

    public Task<ApiResult<SavedIssueFilterResponse>> SaveFilterAsync(SaveIssueFilterRequest request, CancellationToken cancellationToken) =>
        PostAsync<SavedIssueFilterResponse>($"{Root}/filters", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteFilterAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"{Root}/filters/{id:D}", cancellationToken);
}
