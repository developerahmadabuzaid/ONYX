using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Onyx.API.IntegrationTests.Content;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.API.IntegrationTests.Delivery;

/// <summary>
/// The project-management additions through the real API and PostgreSQL: analytics counted from real rows, scheduling, timeline
/// and roadmap, bulk changes, attachments, time logs, saved filters, sprint reports with a rebuilt burndown, velocity and
/// retrospectives. Every endpoint keeps the workspace's permission and project-visibility rules. Fictional sample data only.
/// </summary>
public sealed class DeliveryInsightsApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    private const string Projects = "/api/v1/admin/delivery/projects";
    private const string Issues = "/api/v1/admin/delivery/issues";
    private const string Delivery = "/api/v1/admin/delivery";
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [Fact]
    public async Task New_endpoints_require_workspace_access()
    {
        Fixture.RequireDatabase();
        using var outsider = await SignInWithAsync("outsider", Permissions.News.View);
        foreach (var path in new[] { $"{Delivery}/analytics", $"{Delivery}/roadmap", $"{Delivery}/filters" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync(path, Token)));
            Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.GetAsync(path, Token)));
        }

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.PostAsJsonAsync($"{Issues}/bulk", new BulkIssueRequest([Guid.NewGuid()], "Delete", null, null, null), Token)));
    }

    [Fact]
    public async Task Analytics_count_only_real_visible_work()
    {
        Fixture.RequireDatabase();
        using var admin = await SignInWithAsync("ws-admin", Permissions.Delivery.Access, Permissions.Delivery.Administer);
        using var developer = await SignInWithAsync("developer", Permissions.Delivery.Access);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var project = await CreateProjectAsync(admin, "ANL");
        project = await AddMemberAsync(admin, project, "developer", "Developer");
        var late = await CreateProjectAsync(admin, "LATE", targetDate: today.AddDays(-3));
        var developerId = await UserIdAsync("developer");

        var finished = await CreateIssueAsync(admin, project.Id, "Task", "Finished sample task");
        await ReadAsync<IssueDetailResponse>(await admin.PostAsJsonAsync(Issues, IssueRequest(project.Id, "Bug", "Overdue sample bug") with
        {
            AssigneeId = developerId,
            DueDate = today.AddDays(-1),
        }, Token));
        await CreateIssueAsync(admin, project.Id, "Task", "Pending sample task");
        await CreateIssueAsync(admin, project.Id, "Epic", "Sample epic (not a task)");
        await CreateIssueAsync(admin, late.Id, "Task", "Hidden from the developer");
        await WalkToDoneAsync(admin, project, finished);

        var scoped = await ReadAsync<DeliveryAnalyticsResponse>(await developer.GetAsync($"{Delivery}/analytics?range=7", Token));
        Assert.Equal(7, scoped.RangeDays);
        Assert.Equal(7, scoped.Timeline.Count);
        Assert.Equal("day", scoped.Bucket);
        Assert.Equal(1, scoped.Kpis.TotalProjects);
        Assert.Equal(3, scoped.Kpis.TotalIssues);
        Assert.Equal(3, scoped.Kpis.CreatedInRange);
        Assert.Equal(1, scoped.Kpis.CompletedIssues);
        Assert.Equal(1, scoped.Kpis.CompletedInRange);
        Assert.Equal(1, scoped.Kpis.OverdueIssues);
        Assert.Equal(1, scoped.Kpis.AssignedToMe);
        Assert.Equal(new StatusBreakdownResponse(Completed: 1, InProgress: 0, Pending: 1, Overdue: 1), scoped.Status);
        Assert.Equal(3, scoped.Timeline.Sum(point => point.Created));
        Assert.Equal(1, scoped.Timeline.Sum(point => point.Completed));
        var health = Assert.Single(scoped.Projects);
        Assert.Equal("AtRisk", health.Health);
        var person = Assert.Single(scoped.Team);
        Assert.Equal(developerId, person.UserId);
        Assert.Equal(1, person.Overdue);
        Assert.Single(scoped.MyOpenIssues);
        Assert.All(scoped.RecentActivity, activity => Assert.StartsWith("ANL-", activity.IssueKey, StringComparison.Ordinal));

        // Workspace administrators see every project; the past target date makes LATE delayed.
        var all = await ReadAsync<DeliveryAnalyticsResponse>(await admin.GetAsync($"{Delivery}/analytics?range=90", Token));
        Assert.Equal("week", all.Bucket);
        Assert.Equal(13, all.Timeline.Count);
        Assert.Equal(today, all.Timeline[^1].End);
        Assert.Equal("Delayed", all.Projects.Single(item => item.Key == "LATE").Health);
        Assert.Equal(4, all.Kpis.TotalIssues);

        // Unknown ranges fall back to 30 days; a project the caller cannot see does not exist.
        Assert.Equal(30, (await ReadAsync<DeliveryAnalyticsResponse>(await admin.GetAsync($"{Delivery}/analytics?range=5", Token))).RangeDays);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(developer.GetAsync($"{Delivery}/analytics?projectId={late.Id}", Token)));
        var single = await ReadAsync<DeliveryAnalyticsResponse>(await admin.GetAsync($"{Delivery}/analytics?projectId={late.Id}", Token));
        Assert.Equal(1, single.Kpis.TotalIssues);
    }

    [Fact]
    public async Task Scheduling_timeline_and_roadmap_use_issue_dates_and_blocks_links()
    {
        Fixture.RequireDatabase();
        using var admin = await SignInWithAsync("ws-admin", Permissions.Delivery.Access, Permissions.Delivery.Administer);
        using var viewer = await SignInWithAsync("viewer", Permissions.Delivery.Access);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var project = await CreateProjectAsync(admin, "TML", targetDate: today.AddDays(30));
        project = await AddMemberAsync(admin, project, "viewer", "Viewer");
        var epic = await CreateIssueAsync(admin, project.Id, "Epic", "Sample launch epic");
        var first = await CreateIssueAsync(admin, project.Id, "Task", "Design the sample");
        var second = await CreateIssueAsync(admin, project.Id, "Task", "Build the sample");
        await CreateIssueAsync(admin, project.Id, "Task", "Not scheduled yet");

        epic = await RescheduleAsync(admin, epic, today, today.AddDays(20));
        first = await RescheduleAsync(admin, first, today, today.AddDays(3));
        second = await RescheduleAsync(admin, second, today.AddDays(4), today.AddDays(9));
        Assert.Equal(today.AddDays(4), second.StartDate);
        Assert.Contains(second.History, entry => entry.Field == "dueDate");

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(admin.PostAsJsonAsync(
            $"{Issues}/{first.Summary.Id}/dates", new RescheduleIssueRequest(today.AddDays(5), today, first.Summary.Version), Token)));
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(admin.PostAsJsonAsync(
            $"{Issues}/{first.Summary.Id}/dates", new RescheduleIssueRequest(today, today, first.Summary.Version - 1), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.PostAsJsonAsync(
            $"{Issues}/{first.Summary.Id}/dates", new RescheduleIssueRequest(today, today, first.Summary.Version), Token)));

        // The calendar asks for a due-date window.
        var window = await ReadAsync<PagedResult<IssueListItemResponse>>(await admin.GetAsync(
            $"{Issues}?projectId={project.Id}&dueFrom={today:yyyy-MM-dd}&dueTo={today.AddDays(5):yyyy-MM-dd}", Token));
        Assert.Equal(["TML-2"], window.Items.Select(issue => issue.Key));

        await ReadAsync<IssueDetailResponse>(await admin.PostAsJsonAsync($"{Issues}/{first.Summary.Id}/links", new CreateIssueLinkRequest("TML-3", "Blocks"), Token));
        var timeline = await ReadAsync<TimelineResponse>(await viewer.GetAsync($"{Projects}/{project.Id}/timeline", Token));
        Assert.Equal(3, timeline.Items.Count);
        Assert.Equal(1, timeline.UnscheduledCount);
        Assert.Equal(new TimelineDependencyResponse(first.Summary.Id, second.Summary.Id), Assert.Single(timeline.Dependencies));
        Assert.Equal(today.AddDays(30), timeline.TargetDate);

        var roadmap = await ReadAsync<RoadmapResponse>(await viewer.GetAsync($"{Delivery}/roadmap", Token));
        var entry = Assert.Single(roadmap.Projects);
        Assert.Equal("TML", entry.Key);
        Assert.Equal("Sample launch epic", Assert.Single(entry.Epics).Title);
    }

    [Fact]
    public async Task Bulk_changes_apply_per_issue_with_the_usual_rules()
    {
        Fixture.RequireDatabase();
        using var admin = await SignInWithAsync("ws-admin", Permissions.Delivery.Access, Permissions.Delivery.Administer);
        using var developer = await SignInWithAsync("developer", Permissions.Delivery.Access);
        var own = await CreateProjectAsync(admin, "BLK");
        own = await AddMemberAsync(admin, own, "developer", "Developer");
        var other = await CreateProjectAsync(admin, "RDO");
        other = await AddMemberAsync(admin, other, "developer", "Viewer");
        var developerId = await UserIdAsync("developer");
        var editable = await CreateIssueAsync(admin, own.Id, "Task", "Editable sample");
        var readOnly = await CreateIssueAsync(admin, other.Id, "Task", "Read-only sample");

        var assigned = await ReadAsync<BulkIssueResultResponse>(await developer.PostAsJsonAsync(
            $"{Issues}/bulk", new BulkIssueRequest([editable.Summary.Id, readOnly.Summary.Id], "Assign", developerId, null, null), Token));
        Assert.Equal(1, assigned.Succeeded);
        var refused = Assert.Single(assigned.Failed);
        Assert.Equal(readOnly.Summary.Id, refused.IssueId);
        Assert.Equal("Delivery.Forbidden", refused.Code);

        var labelled = await ReadAsync<BulkIssueResultResponse>(await developer.PostAsJsonAsync(
            $"{Issues}/bulk", new BulkIssueRequest([editable.Summary.Id], "AddLabel", null, null, "Sample-Label"), Token));
        Assert.Equal(1, labelled.Succeeded);
        var prioritised = await ReadAsync<BulkIssueResultResponse>(await developer.PostAsJsonAsync(
            $"{Issues}/bulk", new BulkIssueRequest([editable.Summary.Id], "Priority", null, "Highest", null), Token));
        Assert.Equal(1, prioritised.Succeeded);
        var current = await ReadAsync<IssueDetailResponse>(await developer.GetAsync($"{Issues}/{editable.Summary.Id}", Token));
        Assert.Equal(developerId, current.Summary.AssigneeId);
        Assert.Equal("Highest", current.Summary.Priority);
        Assert.Contains("sample-label", current.Summary.Labels);

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(developer.PostAsJsonAsync($"{Issues}/bulk", new BulkIssueRequest([editable.Summary.Id], "Priority", null, "Urgent", null), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(developer.PostAsJsonAsync($"{Issues}/bulk", new BulkIssueRequest([], "Delete", null, null, null), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(developer.PostAsJsonAsync($"{Issues}/bulk", new BulkIssueRequest([editable.Summary.Id], "Archive", null, null, null), Token)));

        // Deleting needs the project Admin role; the developer is refused, the workspace administrator is not.
        var notDeleted = await ReadAsync<BulkIssueResultResponse>(await developer.PostAsJsonAsync($"{Issues}/bulk", new BulkIssueRequest([editable.Summary.Id], "Delete", null, null, null), Token));
        Assert.Equal(0, notDeleted.Succeeded);
        var deleted = await ReadAsync<BulkIssueResultResponse>(await admin.PostAsJsonAsync($"{Issues}/bulk", new BulkIssueRequest([editable.Summary.Id], "Delete", null, null, null), Token));
        Assert.Equal(1, deleted.Succeeded);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(admin.GetAsync($"{Issues}/{editable.Summary.Id}", Token)));
    }

    [Fact]
    public async Task Attachments_are_checked_by_content_and_served_only_as_downloads()
    {
        Fixture.RequireDatabase();
        using var admin = await SignInWithAsync("ws-admin", Permissions.Delivery.Access, Permissions.Delivery.Administer);
        using var contributor = await SignInWithAsync("contributor", Permissions.Delivery.Access);
        using var other = await SignInWithAsync("other-contributor", Permissions.Delivery.Access);
        using var viewer = await SignInWithAsync("viewer", Permissions.Delivery.Access);
        var project = await CreateProjectAsync(admin, "ATT");
        project = await AddMemberAsync(admin, project, "contributor", "Contributor");
        project = await AddMemberAsync(admin, project, "other-contributor", "Contributor");
        project = await AddMemberAsync(admin, project, "viewer", "Viewer");
        var issue = await CreateIssueAsync(admin, project.Id, "Task", "Sample with files");
        var path = $"{Issues}/{issue.Summary.Id}/attachments";

        var image = await ReadAsync<IssueAttachmentResponse>(await contributor.PostAsync(path, Form(Png, "screen.png"), Token));
        Assert.True(image.IsImage);
        Assert.Equal("image/png", image.ContentType);
        var notes = await ReadAsync<IssueAttachmentResponse>(await contributor.PostAsync(path, Form(Encoding.UTF8.GetBytes("Sample notes, line one.\nLine two."), "../../notes.txt"), Token));
        Assert.Equal("notes.txt", notes.FileName);
        Assert.Equal("text/plain", notes.ContentType);

        // Executables, HTML pretending to be PDF and unknown types are refused; viewers cannot attach.
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(contributor.PostAsync(path, Form(Encoding.UTF8.GetBytes("MZ sample executable"), "tool.exe"), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(contributor.PostAsync(path, Form(Encoding.UTF8.GetBytes("<html>not a pdf</html>"), "report.pdf"), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.PostAsync(path, Form(Png, "viewer.png"), Token)));

        using (var download = await viewer.GetAsync($"{path}/{notes.Id}", Token))
        {
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal("text/plain", download.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal("Sample notes, line one.\nLine two.", await download.Content.ReadAsStringAsync(Token));
        }

        var detail = await ReadAsync<IssueDetailResponse>(await viewer.GetAsync($"{Issues}/{issue.Summary.Id}", Token));
        Assert.Equal(2, detail.Attachments!.Count);
        Assert.All(detail.Attachments, attachment => Assert.False(attachment.CanDelete));

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(other.DeleteAsync($"{path}/{notes.Id}", Token)));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(contributor.DeleteAsync($"{path}/{notes.Id}", Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(viewer.GetAsync($"{path}/{notes.Id}", Token)));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(admin.DeleteAsync($"{path}/{image.Id}", Token)));
        Assert.DoesNotContain(Directory.EnumerateFiles(MediaRoot, "*", SearchOption.AllDirectories), file => file.Contains("delivery", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Time_logs_and_saved_filters_belong_to_their_owner()
    {
        Fixture.RequireDatabase();
        using var admin = await SignInWithAsync("ws-admin", Permissions.Delivery.Access, Permissions.Delivery.Administer);
        using var contributor = await SignInWithAsync("contributor", Permissions.Delivery.Access);
        using var viewer = await SignInWithAsync("viewer", Permissions.Delivery.Access);
        var project = await CreateProjectAsync(admin, "TIME");
        project = await AddMemberAsync(admin, project, "contributor", "Contributor");
        project = await AddMemberAsync(admin, project, "viewer", "Viewer");
        var issue = await CreateIssueAsync(admin, project.Id, "Task", "Sample with time");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var path = $"{Issues}/{issue.Summary.Id}/worklogs";

        var mine = await ReadAsync<WorkLogResponse>(await contributor.PostAsJsonAsync(path, new SaveWorkLogRequest(90, today, "Sample pairing"), Token));
        await ReadAsync<WorkLogResponse>(await admin.PostAsJsonAsync(path, new SaveWorkLogRequest(30, today.AddDays(-1), null), Token));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(contributor.PostAsJsonAsync(path, new SaveWorkLogRequest(0, today, null), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(contributor.PostAsJsonAsync(path, new SaveWorkLogRequest(60, today.AddDays(5), null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.PostAsJsonAsync(path, new SaveWorkLogRequest(15, today, null), Token)));

        var detail = await ReadAsync<IssueDetailResponse>(await contributor.GetAsync($"{Issues}/{issue.Summary.Id}", Token));
        Assert.Equal(120, detail.TimeSpentMinutes);
        Assert.Equal(2, detail.WorkLogs!.Count);
        var adminEntry = detail.WorkLogs.Single(log => log.Minutes == 30);
        Assert.False(adminEntry.CanDelete);
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(contributor.DeleteAsync($"{path}/{adminEntry.Id}", Token)));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(contributor.DeleteAsync($"{path}/{mine.Id}", Token)));

        var saved = await ReadAsync<SavedIssueFilterResponse>(await contributor.PostAsJsonAsync($"{Delivery}/filters",
            new SaveIssueFilterRequest("My overdue bugs", $"type=Bug&overdue=true&projectId={project.Id}"), Token));
        Assert.StartsWith("overdue=true", saved.Query, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(contributor.PostAsJsonAsync($"{Delivery}/filters", new SaveIssueFilterRequest("Bad", "drop=table"), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(contributor.PostAsJsonAsync($"{Delivery}/filters", new SaveIssueFilterRequest(" ", "type=Bug"), Token)));
        Assert.Single(await ReadAsync<IReadOnlyList<SavedIssueFilterResponse>>(await contributor.GetAsync($"{Delivery}/filters", Token)));
        Assert.Empty(await ReadAsync<IReadOnlyList<SavedIssueFilterResponse>>(await viewer.GetAsync($"{Delivery}/filters", Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(viewer.DeleteAsync($"{Delivery}/filters/{saved.Id}", Token)));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(contributor.DeleteAsync($"{Delivery}/filters/{saved.Id}", Token)));
    }

    [Fact]
    public async Task Sprint_report_rebuilds_the_burndown_and_keeps_the_retrospective()
    {
        Fixture.RequireDatabase();
        using var admin = await SignInWithAsync("ws-admin", Permissions.Delivery.Access, Permissions.Delivery.Administer);
        using var developer = await SignInWithAsync("developer", Permissions.Delivery.Access);
        var project = await CreateProjectAsync(admin, "SPR");
        project = await AddMemberAsync(admin, project, "developer", "Developer");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sprint = await ReadAsync<SprintResponse>(await admin.PostAsJsonAsync($"{Projects}/{project.Id}/sprints", new SaveSprintRequest("Sprint 1", "Sample goal", today, today.AddDays(13), null), Token));
        var finished = await CreateIssueAsync(admin, project.Id, "Story", "Finished story", storyPoints: 5, sprintId: sprint.Id);
        await CreateIssueAsync(admin, project.Id, "Task", "Unfinished task", storyPoints: 3, sprintId: sprint.Id);

        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(admin.PutAsJsonAsync(
            $"{Delivery}/sprints/{sprint.Id}/retrospective", new SaveRetrospectiveRequest("Too early", null, null, sprint.Version), Token)));
        var planned = await ReadAsync<SprintReportResponse>(await developer.GetAsync($"{Delivery}/sprints/{sprint.Id}/report", Token));
        Assert.Empty(planned.Burndown);

        // The test clock stands still unless moved: step it so creation, start, work and closing happen at distinct moments.
        Time.Advance(TimeSpan.FromSeconds(1));
        sprint = (await ReadAsync<IReadOnlyList<SprintResponse>>(await admin.GetAsync($"{Projects}/{project.Id}/sprints", Token))).Single();
        var started = await ReadAsync<SprintResponse>(await admin.PostAsJsonAsync($"{Delivery}/sprints/{sprint.Id}/start", new CloseSprintRequest(null, sprint.Version), Token));
        Time.Advance(TimeSpan.FromSeconds(1));
        await WalkToDoneAsync(developer, project, finished);
        Time.Advance(TimeSpan.FromSeconds(1));

        var active = await ReadAsync<SprintReportResponse>(await developer.GetAsync($"{Delivery}/sprints/{started.Id}/report", Token));
        var point = Assert.Single(active.Burndown);
        Assert.Equal(new BurndownPointResponse(today, RemainingPoints: 3, RemainingIssues: 1), point);
        Assert.Equal(2, active.Issues.Count);
        Assert.False(active.Can.ManageSprints);

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(developer.PutAsJsonAsync(
            $"{Delivery}/sprints/{started.Id}/retrospective", new SaveRetrospectiveRequest("Nice", null, null, started.Version), Token)));
        var retro = await ReadAsync<SprintReportResponse>(await admin.PutAsJsonAsync(
            $"{Delivery}/sprints/{started.Id}/retrospective", new SaveRetrospectiveRequest("Pairing on samples", "Smaller stories", "Split stories under 5 points", started.Version), Token));
        Assert.Equal("Smaller stories", retro.Retrospective.ToImprove);
        Assert.NotNull(retro.Retrospective.UpdatedByName);

        // Closing moves the unfinished task out; the burndown still shows it as remaining at the moment of closing.
        var completed = await ReadAsync<SprintResponse>(await admin.PostAsJsonAsync($"{Delivery}/sprints/{started.Id}/complete", new CloseSprintRequest(null, retro.Sprint.Version), Token));
        var closed = await ReadAsync<SprintReportResponse>(await developer.GetAsync($"{Delivery}/sprints/{completed.Id}/report", Token));
        Assert.Equal(3, Assert.Single(closed.Burndown).RemainingPoints);
        Assert.Equal("Split stories under 5 points", closed.Retrospective.ActionItems);

        var velocity = await ReadAsync<IReadOnlyList<SprintVelocityResponse>>(await developer.GetAsync($"{Projects}/{project.Id}/velocity", Token));
        var closedSprint = Assert.Single(velocity);
        Assert.Equal(8, closedSprint.CommittedPoints);
        Assert.Equal(5, closedSprint.CompletedPoints);
    }

    private static MultipartFormDataContent Form(byte[] bytes, string fileName)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return form;
    }

    private static async Task<IssueDetailResponse> RescheduleAsync(HttpClient client, IssueDetailResponse issue, DateOnly? start, DateOnly? due) =>
        await ReadAsync<IssueDetailResponse>(await client.PostAsJsonAsync(
            $"{Issues}/{issue.Summary.Id}/dates", new RescheduleIssueRequest(start, due, issue.Summary.Version), Token));

    private static async Task WalkToDoneAsync(HttpClient client, DeliveryProjectResponse project, IssueDetailResponse issue)
    {
        var current = await ReadAsync<IssueDetailResponse>(await client.GetAsync($"{Issues}/{issue.Summary.Id}", Token));
        foreach (var key in new[] { "todo", "in-progress", "in-review", "testing", "done" })
        {
            current = await ReadAsync<IssueDetailResponse>(await client.PostAsJsonAsync(
                $"{Issues}/{issue.Summary.Id}/transition", new TransitionIssueRequest(project.Statuses.Single(status => status.SystemKey == key).Id, current.Summary.Version), Token));
        }
    }

    private static SaveDeliveryProjectRequest ProjectRequest(string key, DateOnly? targetDate) =>
        new(key, $"Sample {key} project", "Fictional project for tests.", "AiAgent", "Internal", null, "Active", "High", null, targetDate, null, null);

    private static SaveIssueRequest IssueRequest(Guid projectId, string type, string title, int? storyPoints = null, Guid? sprintId = null) =>
        new(projectId, type, title, null, "Medium", null, storyPoints, null, null, null, null, null, null, null, sprintId, null, null, null, null, null);

    private static async Task<DeliveryProjectResponse> CreateProjectAsync(HttpClient client, string key, DateOnly? targetDate = null) =>
        await ReadAsync<DeliveryProjectResponse>(await client.PostAsJsonAsync(Projects, ProjectRequest(key, targetDate), Token));

    private static async Task<IssueDetailResponse> CreateIssueAsync(HttpClient client, Guid projectId, string type, string title, int? storyPoints = null, Guid? sprintId = null) =>
        await ReadAsync<IssueDetailResponse>(await client.PostAsJsonAsync(Issues, IssueRequest(projectId, type, title, storyPoints, sprintId), Token));

    private async Task<DeliveryProjectResponse> AddMemberAsync(HttpClient admin, DeliveryProjectResponse project, string name, string role) =>
        await ReadAsync<DeliveryProjectResponse>(await admin.PostAsJsonAsync(
            $"{Projects}/{project.Id}/members", new SaveProjectMemberRequest(await UserIdAsync(name), role, project.Version), Token));

    private async Task<Guid> UserIdAsync(string name) =>
        (await QueryAsync(db => db.Users.Where(user => user.Email == $"{name}@onyx.test").Select(user => user.Id))).Single();
}
