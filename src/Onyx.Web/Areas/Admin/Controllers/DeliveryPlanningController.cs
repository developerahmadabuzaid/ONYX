using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Onyx.Contracts.Delivery;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Delivery;
using Onyx.Web.Resources;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Planning and insight screens of the delivery workspace: reports, roadmap, calendar, project timelines, sprints with burndown
/// and retrospectives, plus issue attachments, time logs, bulk changes, saved filters and the command-palette search. Everything
/// is forwarded to Onyx.API, which enforces the permission and the caller's project role.
/// </summary>
[Route("admin/delivery")]
public sealed class DeliveryPlanningController(IAdminDeliveryApiClient api, TimeProvider clock, IStringLocalizer<SharedResource> localizer) : CmsControllerBase
{
    private const string Views = "~/Areas/Admin/Views/Delivery/";
    private const int CalendarPageSize = 100;

    [HttpGet("reports")]
    public async Task<IActionResult> Reports(int range = 30, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var analytics = await api.AnalyticsAsync(range, projectId, cancellationToken);
        if (Failed(analytics) is { } failed)
        {
            return failed;
        }

        var projects = await api.ListProjectsAsync(cancellationToken);
        return View(Views + "Reports.cshtml", new ReportsPage(analytics.Value!, projects.Value ?? [], projectId));
    }

    [HttpGet("roadmap")]
    public async Task<IActionResult> Roadmap(CancellationToken cancellationToken)
    {
        var roadmap = await api.RoadmapAsync(cancellationToken);
        return Failed(roadmap) ?? View(Views + "Roadmap.cshtml", new RoadmapPage(roadmap.Value!));
    }

    /// <summary>Due dates by month (default) or week. <paramref name="date"/> is any day in the period to show (yyyy-MM-dd).</summary>
    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar(string? view, string? date, Guid? projectId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var anchor = DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : today;
        var week = string.Equals(view, "week", StringComparison.OrdinalIgnoreCase);
        DateOnly from;
        DateOnly to;
        if (week)
        {
            from = anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7));
            to = from.AddDays(6);
        }
        else
        {
            var first = new DateOnly(anchor.Year, anchor.Month, 1);
            from = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
            to = from.AddDays(41);
        }

        var issues = await api.SearchIssuesAsync(
            new IssueQuery(ProjectId: projectId, Sort: "due", PageSize: CalendarPageSize, DueFrom: from, DueTo: to), cancellationToken);
        if (Failed(issues) is { } failed)
        {
            return failed;
        }

        var projects = await api.ListProjectsAsync(cancellationToken);
        return View(Views + "Calendar.cshtml", new CalendarPage(
            week ? "week" : "month", anchor, from, to, today, issues.Value!.Items, projects.Value ?? [], projectId, issues.Value.TotalCount > issues.Value.Items.Count));
    }

    /// <summary>
    /// Calendar drag and drop (JSON for the script) and the no-script date form. The start date moves with the due date so the
    /// issue keeps its length.
    /// </summary>
    [HttpPost("issues/{id:guid}/dates")]
    public async Task<IActionResult> Reschedule(Guid id, DateOnly? startDate, DateOnly? dueDate, uint version, string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await api.RescheduleIssueAsync(id, new RescheduleIssueRequest(startDate, dueDate, version), cancellationToken);
        if (Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            if (result.IsUnavailable)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ok = false, message = localizer["Delivery.ApiUnavailable"].Value });
            }

            return result.IsSuccess
                ? Json(new { ok = true, version = result.Value!.Summary.Version })
                : StatusCode((int)result.StatusCode, new { ok = false, message = Describe(result.Problem) });
        }

        return After(result, LocalRedirect(SafeReturn(returnUrl, Url.Action("Issue", "Delivery", new { id })!)));
    }

    [HttpGet("projects/{id:guid}/timeline")]
    public async Task<IActionResult> Timeline(Guid id, CancellationToken cancellationToken)
    {
        var timeline = await api.TimelineAsync(id, cancellationToken);
        return Failed(timeline) ?? View(Views + "Timeline.cshtml", new TimelinePage(timeline.Value!));
    }

    [HttpGet("projects/{id:guid}/sprints")]
    public async Task<IActionResult> ProjectSprints(Guid id, CancellationToken cancellationToken)
    {
        var project = await api.GetProjectAsync(id, cancellationToken);
        if (Failed(project) is { } failed)
        {
            return failed;
        }

        var sprints = await api.SprintsAsync(id, includeClosed: true, cancellationToken);
        var velocity = await api.VelocityAsync(id, cancellationToken);
        return View(Views + "ProjectSprints.cshtml", new ProjectSprintsPage(project.Value!, sprints.Value ?? [], velocity.Value ?? []));
    }

    [HttpGet("sprints")]
    public async Task<IActionResult> Sprints(CancellationToken cancellationToken)
    {
        var analytics = await api.AnalyticsAsync(30, null, cancellationToken);
        if (Failed(analytics) is { } failed)
        {
            return failed;
        }

        var projects = await api.ListProjectsAsync(cancellationToken);
        return View(Views + "Sprints.cshtml", new SprintsOverviewPage(analytics.Value!.ActiveSprints, projects.Value ?? []));
    }

    [HttpGet("sprints/{sprintId:guid}")]
    public async Task<IActionResult> Sprint(Guid sprintId, CancellationToken cancellationToken)
    {
        var report = await api.SprintReportAsync(sprintId, cancellationToken);
        return Failed(report) ?? View(Views + "Sprint.cshtml", new SprintReportPage(report.Value!));
    }

    [HttpPost("sprints/{sprintId:guid}/retrospective")]
    public async Task<IActionResult> SaveRetrospective(Guid sprintId, string? wentWell, string? toImprove, string? actionItems, uint version, CancellationToken cancellationToken) =>
        After(await api.SaveRetrospectiveAsync(sprintId, new SaveRetrospectiveRequest(wentWell, toImprove, actionItems, version), cancellationToken),
            Redirect(Url.Action(nameof(Sprint), new { sprintId }) + "#retrospective"), "Delivery.RetrospectiveSaved");

    /// <summary>One change for the selected issues; the outcome (including refusals per issue) is shown after the redirect.</summary>
    [HttpPost("issues/bulk")]
    public async Task<IActionResult> Bulk(List<Guid>? issueIds, string? bulkAction, Guid? assigneeId, string? priority, string? label, string? returnUrl, CancellationToken cancellationToken)
    {
        var back = LocalRedirect(SafeReturn(returnUrl, Url.Action("Issues", "Delivery")!));
        if (issueIds is null || issueIds.Count == 0)
        {
            return ShowError(localizer["Delivery.Bulk.NothingSelected"], back);
        }

        var result = await api.BulkAsync(new BulkIssueRequest(issueIds, bulkAction, assigneeId, Blank(priority), Blank(label)), cancellationToken);
        if (!result.IsSuccess)
        {
            return After(result, back);
        }

        var outcome = result.Value!;
        if (outcome.Failed.Count == 0)
        {
            TempData[NoticeKey] = "Delivery.Bulk.Done";
            return back;
        }

        var refused = string.Join(", ", outcome.Failed.Select(failure => failure.Key ?? failure.IssueId.ToString("D")));
        return ShowError(string.Format(CultureInfo.CurrentCulture, localizer["Delivery.Bulk.Partial"], outcome.Succeeded, outcome.Failed.Count, refused), back);
    }

    [HttpPost("issues/{id:guid}/attachments")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> Attach(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        var back = Redirect(Url.Action("Issue", "Delivery", new { id }) + "#attachments");
        if (file is null || file.Length == 0)
        {
            return ShowError(localizer["Delivery.Attachment.Choose"], back);
        }

        await using var stream = file.OpenReadStream();
        return After(await api.UploadAttachmentAsync(id, stream, file.FileName, file.ContentType, cancellationToken), back, "Delivery.Attachment.Added");
    }

    /// <summary>Attachments are always downloads (images may also be shown as thumbnails); never cached.</summary>
    [HttpGet("issues/{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Attachment(Guid id, Guid attachmentId, bool inline, CancellationToken cancellationToken)
    {
        var file = await api.OpenAttachmentAsync(id, attachmentId, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        Response.RegisterForDisposeAsync(file);
        Response.Headers.CacheControl = "private, no-store";
        var type = file.ContentType.Split(';')[0].Trim();
        var isImage = type is "image/jpeg" or "image/png" or "image/webp" or "image/gif";
        return inline && isImage ? File(file.Content, type) : File(file.Content, type, file.FileName ?? "attachment");
    }

    [HttpPost("issues/{id:guid}/attachments/{attachmentId:guid}/delete")]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken) =>
        After(await api.DeleteAttachmentAsync(id, attachmentId, cancellationToken), Redirect(Url.Action("Issue", "Delivery", new { id }) + "#attachments"), "Delivery.Attachment.Removed");

    [HttpPost("issues/{id:guid}/worklogs")]
    public async Task<IActionResult> LogWork(Guid id, int? hours, int? minutes, DateOnly? workDate, string? note, CancellationToken cancellationToken)
    {
        var total = (Math.Max(hours ?? 0, 0) * 60) + Math.Max(minutes ?? 0, 0);
        var day = workDate ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return After(await api.AddWorkLogAsync(id, new SaveWorkLogRequest(total, day, note), cancellationToken),
            Redirect(Url.Action("Issue", "Delivery", new { id }) + "#time"), "Delivery.Time.Logged");
    }

    [HttpPost("issues/{id:guid}/worklogs/{workLogId:guid}/delete")]
    public async Task<IActionResult> DeleteWorkLog(Guid id, Guid workLogId, CancellationToken cancellationToken) =>
        After(await api.DeleteWorkLogAsync(id, workLogId, cancellationToken), Redirect(Url.Action("Issue", "Delivery", new { id }) + "#time"), "Delivery.Time.Removed");

    [HttpPost("filters")]
    public async Task<IActionResult> SaveFilter(string? name, string? query, CancellationToken cancellationToken)
    {
        var result = await api.SaveFilterAsync(new SaveIssueFilterRequest(name, query), cancellationToken);
        var target = Url.Action("Issues", "Delivery") + (result.IsSuccess ? "?" + result.Value!.Query : string.Empty);
        return After(result, LocalRedirect(target), "Delivery.Filter.Saved");
    }

    [HttpPost("filters/{id:guid}/delete")]
    public async Task<IActionResult> DeleteFilter(Guid id, CancellationToken cancellationToken) =>
        After(await api.DeleteFilterAsync(id, cancellationToken), RedirectToAction("Issues", "Delivery"), "Delivery.Filter.Removed");

    /// <summary>Issue search for the command palette: key and title matches in the caller's visible projects, as JSON.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(string? q, CancellationToken cancellationToken)
    {
        var text = q?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length < 2)
        {
            return Json(Array.Empty<object>());
        }

        var result = await api.SearchIssuesAsync(new IssueQuery(Text: text.Length > 100 ? text[..100] : text, PageSize: 8), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.StatusCode == System.Net.HttpStatusCode.Forbidden ? Forbid() : Json(Array.Empty<object>());
        }

        Response.Headers.CacheControl = "no-store";
        return Json(result.Value!.Items.Select(issue => new { key = issue.Key, title = issue.Title, url = Url.Action("Issue", "Delivery", new { id = issue.Id }) }));
    }

    private string SafeReturn(string? returnUrl, string fallback) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith("/admin/", StringComparison.Ordinal) ? returnUrl : fallback;
}
