using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Delivery;
using Onyx.Web.Authentication;
using Onyx.Web.Resources;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// The delivery workspace screens: dashboard, projects, board, backlog and sprints, issue search, My Work and issue details. Every
/// form posts back here and is forwarded to Onyx.API, which enforces the permission and the caller's project role; this controller
/// only hides actions the caller cannot take and turns API outcomes into pages.
/// </summary>
[Route("admin/delivery")]
public sealed class DeliveryController(IAdminDeliveryApiClient api, CurrentAdminContext currentAdmin, IStringLocalizer<SharedResource> localizer) : CmsControllerBase
{
    private const int PageSize = 25;

    /// <summary>The workspace dashboard: real figures and charts for the last 7, 30 or 90 days.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(int range = 30, CancellationToken cancellationToken = default)
    {
        var result = await api.AnalyticsAsync(range, null, cancellationToken);
        if (Failed(result) is { } failed)
        {
            return failed;
        }

        var unread = (ViewData[AdminShellFilter.ViewDataKey] as AdminShellModel)?.UnreadNotifications ?? 0;
        return View("Dashboard", new WorkspacePage(result.Value!, unread, await IsAdministratorAsync(cancellationToken)));
    }

    [HttpGet("projects")]
    public async Task<IActionResult> Projects(CancellationToken cancellationToken)
    {
        var result = await api.ListProjectsAsync(cancellationToken);
        return Failed(result) ?? View(new ProjectsPage(result.Value!, await IsAdministratorAsync(cancellationToken)));
    }

    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject(ProjectForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var result = await api.CreateProjectAsync(form.ToRequest(null), cancellationToken);
        return result.IsSuccess
            ? After(result, RedirectToAction(nameof(Board), new { id = result.Value!.Id }), "Delivery.ProjectCreated")
            : After(result, RedirectToAction(nameof(Projects)));
    }

    [HttpGet("projects/{id:guid}")]
    public async Task<IActionResult> Project(Guid id, CancellationToken cancellationToken)
    {
        var project = await api.GetProjectAsync(id, cancellationToken);
        if (Failed(project) is { } failed)
        {
            return failed;
        }

        IReadOnlyList<DeliveryUserResponse> eligible = [];
        if (project.Value!.Can.ManageProject)
        {
            var users = await api.EligibleUsersAsync(cancellationToken);
            eligible = users.IsSuccess ? users.Value!.Where(user => project.Value.Members.All(member => member.UserId != user.UserId)).ToList() : [];
        }

        var analytics = await api.AnalyticsAsync(30, id, cancellationToken);
        return View(new ProjectPage(project.Value, eligible, analytics.IsSuccess ? analytics.Value : null));
    }

    [HttpPost("projects/{id:guid}")]
    public async Task<IActionResult> UpdateProject(Guid id, ProjectForm form, uint version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        return After(await api.UpdateProjectAsync(id, form.ToRequest(version), cancellationToken), RedirectToAction(nameof(Project), new { id }));
    }

    [HttpPost("projects/{id:guid}/members")]
    public async Task<IActionResult> SaveMember(Guid id, Guid userId, string? role, uint version, CancellationToken cancellationToken) =>
        After(await api.SaveMemberAsync(id, new SaveProjectMemberRequest(userId, role, version), cancellationToken), RedirectToAction(nameof(Project), new { id }));

    [HttpPost("projects/{id:guid}/members/{userId:guid}/remove")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, uint version, CancellationToken cancellationToken) =>
        After(await api.RemoveMemberAsync(id, userId, version, cancellationToken), RedirectToAction(nameof(Project), new { id }));

    [HttpPost("projects/{id:guid}/statuses/{statusId:guid}/wip")]
    public async Task<IActionResult> SetWipLimit(Guid id, Guid statusId, int? wipLimit, uint version, CancellationToken cancellationToken) =>
        After(await api.SetWipLimitAsync(id, statusId, new SetWipLimitRequest(wipLimit, version), cancellationToken), RedirectToAction(nameof(Project), new { id }));

    [HttpGet("projects/{id:guid}/board")]
    public async Task<IActionResult> Board(Guid id, string? assignee, string? type, string? priority, string? label, string? text, CancellationToken cancellationToken)
    {
        var board = await api.BoardAsync(id, cancellationToken);
        if (Failed(board) is { } failed)
        {
            return failed;
        }

        var filter = new BoardFilter(assignee, type, priority, label, text);
        return View(new BoardPage(board.Value!, filter));
    }

    [HttpGet("projects/{id:guid}/backlog")]
    public async Task<IActionResult> Backlog(Guid id, CancellationToken cancellationToken)
    {
        var backlog = await api.BacklogAsync(id, cancellationToken);
        if (Failed(backlog) is { } failed)
        {
            return failed;
        }

        var history = await api.SprintsAsync(id, includeClosed: true, cancellationToken);
        var closed = history.IsSuccess ? history.Value!.Where(sprint => sprint.State is "Completed" or "Cancelled").ToList() : [];
        return View(new BacklogPage(backlog.Value!, closed));
    }

    [HttpPost("projects/{id:guid}/sprints")]
    public async Task<IActionResult> CreateSprint(Guid id, SprintForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        return After(await api.CreateSprintAsync(id, form.ToRequest(null), cancellationToken), RedirectToAction(nameof(Backlog), new { id }), "Delivery.SprintCreated");
    }

    [HttpPost("projects/{id:guid}/sprints/{sprintId:guid}")]
    public async Task<IActionResult> PlanSprint(Guid id, Guid sprintId, SprintForm form, uint version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        return After(await api.PlanSprintAsync(sprintId, form.ToRequest(version), cancellationToken), RedirectToAction(nameof(Backlog), new { id }));
    }

    [HttpPost("projects/{id:guid}/sprints/{sprintId:guid}/{command:regex(^(start|complete|cancel)$)}")]
    public async Task<IActionResult> ChangeSprint(Guid id, Guid sprintId, string command, Guid? moveTo, uint version, CancellationToken cancellationToken) =>
        After(await api.ChangeSprintAsync(sprintId, command, new CloseSprintRequest(moveTo, version), cancellationToken), RedirectToAction(nameof(Backlog), new { id }));

    [HttpGet("issues")]
    public async Task<IActionResult> Issues(IssueFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return await IssueListAsync(filter, mine: false, cancellationToken);
    }

    [HttpGet("my-work")]
    public async Task<IActionResult> MyWork(IssueFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        filter.Status ??= "Open";
        return await IssueListAsync(filter, mine: true, cancellationToken);
    }

    [HttpGet("issues/new")]
    public async Task<IActionResult> NewIssue(Guid? projectId, Guid? parentId, Guid? sprintId, string? type, CancellationToken cancellationToken)
    {
        var projects = await api.ListProjectsAsync(cancellationToken);
        if (Failed(projects) is { } failed)
        {
            return failed;
        }

        var selected = projectId ?? (projects.Value!.Count == 1 ? projects.Value[0].Id : null);
        var options = selected is { } chosen ? await IssueOptionsAsync(chosen, cancellationToken) : null;
        var form = new IssueForm
        {
            ProjectId = selected,
            ParentId = parentId,
            SprintId = sprintId,
            Type = parentId is not null ? "Subtask" : type ?? "Task",
            Priority = "Medium",
        };
        return View(new NewIssuePage(projects.Value!, options, form));
    }

    [HttpPost("issues/new")]
    public async Task<IActionResult> CreateIssue(IssueForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var result = await api.CreateIssueAsync(form.ToRequest(), cancellationToken);
        if (result.IsSuccess)
        {
            return After(result, RedirectToAction(nameof(Issue), new { id = result.Value!.Summary.Id }), "Delivery.IssueCreated");
        }

        AddProblem(result);
        var projects = await api.ListProjectsAsync(cancellationToken);
        var options = form.ProjectId is { } projectId ? await IssueOptionsAsync(projectId, cancellationToken) : null;
        return View("NewIssue", new NewIssuePage(projects.Value ?? [], options, form));
    }

    [HttpGet("issues/{id:guid}")]
    public async Task<IActionResult> Issue(Guid id, CancellationToken cancellationToken)
    {
        var issue = await api.GetIssueAsync(id, cancellationToken);
        if (Failed(issue) is { } failed)
        {
            return failed;
        }

        var options = await IssueOptionsAsync(issue.Value!.Summary.ProjectId, cancellationToken);
        return View(new IssuePage(issue.Value, options));
    }

    [HttpPost("issues/{id:guid}")]
    public async Task<IActionResult> UpdateIssue(Guid id, IssueForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        return After(await api.UpdateIssueAsync(id, form.ToRequest(), cancellationToken), RedirectToAction(nameof(Issue), new { id }));
    }

    [HttpPost("issues/{id:guid}/transition")]
    public async Task<IActionResult> TransitionIssue(Guid id, Guid statusId, uint version, CancellationToken cancellationToken) =>
        After(await api.TransitionIssueAsync(id, new TransitionIssueRequest(statusId, version), cancellationToken), RedirectToAction(nameof(Issue), new { id }));

    /// <summary>Backlog "move to" without JavaScript: sets the sprint (empty = backlog) and appends the issue to that list.</summary>
    [HttpPost("issues/{id:guid}/schedule")]
    public async Task<IActionResult> ScheduleIssue(Guid id, Guid projectId, Guid? sprintId, uint version, CancellationToken cancellationToken) =>
        After(await api.MoveIssueAsync(id, new MoveIssueRequest(null, true, sprintId, null, version), cancellationToken), RedirectToAction(nameof(Backlog), new { id = projectId }));

    /// <summary>
    /// Drag and drop from the board or backlog (delivery-board.js). Posts a normal form with the antiforgery token and answers JSON,
    /// so the page can keep the new order or roll the card back and show the API's message.
    /// </summary>
    [HttpPost("issues/{id:guid}/move")]
    public async Task<IActionResult> MoveIssue(Guid id, Guid? statusId, bool changeSprint, Guid? sprintId, Guid? beforeIssueId, uint version, CancellationToken cancellationToken)
    {
        var result = await api.MoveIssueAsync(id, new MoveIssueRequest(statusId, changeSprint, sprintId, beforeIssueId, version), cancellationToken);
        if (result.IsUnavailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ok = false, message = T("Delivery.ApiUnavailable") });
        }

        return result.IsSuccess
            ? Json(new { ok = true, version = result.Value!.Summary.Version })
            : StatusCode((int)result.StatusCode, new { ok = false, message = Describe(result.Problem) });
    }

    [HttpPost("issues/{id:guid}/delete")]
    public async Task<IActionResult> DeleteIssue(Guid id, Guid projectId, CancellationToken cancellationToken)
    {
        var result = await api.DeleteIssueAsync(id, cancellationToken);
        return result.IsSuccess
            ? After(result, RedirectToAction(nameof(Board), new { id = projectId }), "Delivery.IssueDeleted")
            : After(result, RedirectToAction(nameof(Issue), new { id }));
    }

    [HttpPost("issues/{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, string? body, Guid? parentCommentId, CancellationToken cancellationToken) =>
        After(await api.AddCommentAsync(id, new SaveCommentRequest(body, parentCommentId), cancellationToken), Redirect(Url.Action(nameof(Issue), new { id }) + "#comments"), "Delivery.CommentAdded");

    [HttpPost("issues/{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> EditComment(Guid id, Guid commentId, string? body, CancellationToken cancellationToken) =>
        After(await api.EditCommentAsync(id, commentId, new SaveCommentRequest(body, null), cancellationToken), Redirect(Url.Action(nameof(Issue), new { id }) + "#comments"));

    [HttpPost("issues/{id:guid}/comments/{commentId:guid}/delete")]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken cancellationToken) =>
        After(await api.DeleteCommentAsync(id, commentId, cancellationToken), Redirect(Url.Action(nameof(Issue), new { id }) + "#comments"));

    [HttpPost("issues/{id:guid}/links")]
    public async Task<IActionResult> AddLink(Guid id, string? targetKey, string? type, CancellationToken cancellationToken) =>
        After(await api.AddLinkAsync(id, new CreateIssueLinkRequest(targetKey, type), cancellationToken), RedirectToAction(nameof(Issue), new { id }));

    [HttpPost("issues/{id:guid}/links/{linkId:guid}/delete")]
    public async Task<IActionResult> RemoveLink(Guid id, Guid linkId, CancellationToken cancellationToken) =>
        After(await api.RemoveLinkAsync(id, linkId, cancellationToken), RedirectToAction(nameof(Issue), new { id }));

    [HttpPost("issues/{id:guid}/watch")]
    public async Task<IActionResult> Watch(Guid id, bool watching, CancellationToken cancellationToken) =>
        After(await api.SetWatchingAsync(id, watching, cancellationToken), RedirectToAction(nameof(Issue), new { id }));

    private async Task<IActionResult> IssueListAsync(IssueFilter filter, bool mine, CancellationToken cancellationToken)
    {
        var query = new IssueQuery(
            ProjectId: filter.ProjectId,
            EpicId: filter.EpicId,
            AssigneeId: mine ? null : filter.AssigneeId,
            Mine: mine,
            StatusCategory: filter.Status,
            Type: filter.Type,
            Priority: filter.Priority,
            Label: filter.Label,
            Text: filter.Text,
            OverdueOnly: filter.Overdue,
            Sort: filter.Sort,
            Page: Math.Max(1, filter.Page),
            PageSize: PageSize);
        var issues = await api.SearchIssuesAsync(query, cancellationToken);
        if (Failed(issues) is { } failed)
        {
            return failed;
        }

        var projects = await api.ListProjectsAsync(cancellationToken);
        var saved = await api.SavedFiltersAsync(cancellationToken);
        var people = await api.EligibleUsersAsync(cancellationToken);
        return View("Issues", new IssuesPage(issues.Value!, projects.Value ?? [], filter, mine, saved.Value ?? [], people.Value ?? []));
    }

    private async Task<IssueOptions?> IssueOptionsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await api.GetProjectAsync(projectId, cancellationToken);
        if (!project.IsSuccess)
        {
            return null;
        }

        var epics = await api.SearchIssuesAsync(new IssueQuery(ProjectId: projectId, Type: "Epic", Sort: "key", PageSize: 100), cancellationToken);
        var sprints = await api.SprintsAsync(projectId, includeClosed: false, cancellationToken);
        return new IssueOptions(
            project.Value!,
            epics.IsSuccess ? epics.Value!.Items : [],
            sprints.IsSuccess ? sprints.Value! : []);
    }

    private async Task<bool> IsAdministratorAsync(CancellationToken cancellationToken) =>
        (await currentAdmin.GetAsync(cancellationToken)).Permissions.Contains(Permissions.Delivery.Administer, StringComparer.Ordinal);

    private string T(string key) => localizer[key];
}
