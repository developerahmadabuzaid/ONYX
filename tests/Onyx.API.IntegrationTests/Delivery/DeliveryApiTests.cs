using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Content;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.API.IntegrationTests.Delivery;

/// <summary>
/// The delivery workspace through the real API and PostgreSQL: permissions and project isolation, issue keys, workflow
/// transitions, persistent ordering, hierarchy rules, sprint lifecycle, search and dashboard. All data is fictional sample data.
/// </summary>
public sealed class DeliveryApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    private const string Projects = "/api/v1/admin/delivery/projects";
    private const string Issues = "/api/v1/admin/delivery/issues";
    private const string Delivery = "/api/v1/admin/delivery";

    [Fact]
    public async Task Workspace_requires_permission_and_only_administrators_create_projects()
    {
        Fixture.RequireDatabase();
        using var outsider = await SignInWithAsync("outsider", Permissions.News.View);
        using var member = await SignInWithAsync("member", Permissions.Delivery.Access);
        using var admin = await SignInWithAsync("ws-admin", Permissions.Delivery.Access, Permissions.Delivery.Administer);

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync(Projects, Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(outsider.GetAsync(Projects, Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(member.PostAsJsonAsync(Projects, ProjectRequest("AI"), Token)));

        var project = await CreateProjectAsync(admin, "ai");
        Assert.Equal("AI", project.Key);
        Assert.Equal("Admin", project.MyRole);
        Assert.Equal(6, project.Statuses.Count);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(admin.PostAsJsonAsync(Projects, ProjectRequest("AI"), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(admin.PostAsJsonAsync(Projects, ProjectRequest("1X"), Token)));

        // A user with workspace access but no membership cannot see the project or its issues.
        var issue = await CreateIssueAsync(admin, project.Id, "Task", "Prepare the sample agent");
        Assert.Empty(await ReadAsync<IReadOnlyList<DeliveryProjectListItemResponse>>(await member.GetAsync(Projects, Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(member.GetAsync($"{Projects}/{project.Id}", Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(member.GetAsync($"{Issues}/{issue.Summary.Id}", Token)));
        Assert.Equal(0, (await ReadAsync<PagedResult<IssueListItemResponse>>(await member.GetAsync(Issues, Token))).TotalCount);
    }

    [Fact]
    public async Task Issue_keys_roles_and_workflow_transitions_are_enforced()
    {
        Fixture.RequireDatabase();
        using var admin = await SignInWithAsync("ws-admin", Permissions.Delivery.Access, Permissions.Delivery.Administer);
        using var contributor = await SignInWithAsync("contributor", Permissions.Delivery.Access);
        using var developer = await SignInWithAsync("developer", Permissions.Delivery.Access);
        using var viewer = await SignInWithAsync("viewer", Permissions.Delivery.Access);
        var project = await CreateProjectAsync(admin, "WEB");
        project = await AddMemberAsync(admin, project, "contributor", "Contributor");
        project = await AddMemberAsync(admin, project, "developer", "Developer");
        project = await AddMemberAsync(admin, project, "viewer", "Viewer");

        var first = await CreateIssueAsync(contributor, project.Id, "Bug", "Login form fails on sample data");
        var second = await CreateIssueAsync(developer, project.Id, "Story", "Sample checkout story");
        Assert.Equal("WEB-1", first.Summary.Key);
        Assert.Equal("WEB-2", second.Summary.Key);
        Assert.Equal("backlog", first.Summary.StatusKey);
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.PostAsJsonAsync(Issues, IssueRequest(project.Id, "Task", "Viewer issue"), Token)));

        var todo = Status(project, "todo");
        var done = Status(project, "done");
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(contributor.PostAsJsonAsync(
            $"{Issues}/{first.Summary.Id}/transition", new TransitionIssueRequest(todo.Id, first.Summary.Version), Token)));

        // Backlog → Done is not in the default workflow.
        using (var invalid = await developer.PostAsJsonAsync($"{Issues}/{first.Summary.Id}/transition", new TransitionIssueRequest(done.Id, first.Summary.Version), Token))
        {
            Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
            Assert.Contains("Delivery.TransitionNotAllowed", await invalid.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }

        var moved = await ReadAsync<IssueDetailResponse>(await developer.PostAsJsonAsync(
            $"{Issues}/{first.Summary.Id}/transition", new TransitionIssueRequest(todo.Id, first.Summary.Version), Token));
        Assert.Equal("todo", moved.Summary.StatusKey);
        Assert.Contains(moved.History, entry => entry.Field == "status" && entry.OldValue == "Backlog" && entry.NewValue == "To Do");

        // A stale version is rejected instead of overwriting someone else's change.
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(developer.PostAsJsonAsync(
            $"{Issues}/{first.Summary.Id}/transition", new TransitionIssueRequest(Status(project, "in-progress").Id, first.Summary.Version), Token)));

        var commented = await ReadAsync<IssueDetailResponse>(await contributor.PostAsJsonAsync($"{Issues}/{first.Summary.Id}/comments", new SaveCommentRequest("Reproduced with sample data.", null), Token));
        Assert.Single(commented.Comments);
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.PostAsJsonAsync($"{Issues}/{first.Summary.Id}/comments", new SaveCommentRequest("Viewer comment", null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(developer.PutAsJsonAsync(
            $"{Issues}/{first.Summary.Id}/comments/{commented.Comments[0].Id}", new SaveCommentRequest("Edited by someone else", null), Token)));

        var linked = await ReadAsync<IssueDetailResponse>(await developer.PostAsJsonAsync($"{Issues}/{first.Summary.Id}/links", new CreateIssueLinkRequest("web-2", "Blocks"), Token));
        Assert.Contains(linked.Links, link => link.IssueKey == "WEB-2" && link.Direction == "outward" && link.Type == "Blocks");
        var target = await ReadAsync<IssueDetailResponse>(await developer.GetAsync($"{Issues}/{second.Summary.Id}", Token));
        Assert.Contains(target.Links, link => link.IssueKey == "WEB-1" && link.Direction == "inward");
    }

    [Fact]
    public async Task Board_moves_persist_order_and_hierarchy_rules_hold()
    {
        Fixture.RequireDatabase();
        using var admin = await SignInWithAsync("ws-admin", Permissions.Delivery.Access, Permissions.Delivery.Administer);
        var project = await CreateProjectAsync(admin, "OPS");
        var one = await CreateIssueAsync(admin, project.Id, "Task", "One");
        var two = await CreateIssueAsync(admin, project.Id, "Task", "Two");
        var three = await CreateIssueAsync(admin, project.Id, "Task", "Three");
        var backlog = Status(project, "backlog");

        await ReadAsync<IssueDetailResponse>(await admin.PostAsJsonAsync(
            $"{Issues}/{three.Summary.Id}/move", new MoveIssueRequest(backlog.Id, false, null, one.Summary.Id, three.Summary.Version), Token));
        var board = await ReadAsync<BoardResponse>(await admin.GetAsync($"{Projects}/{project.Id}/board", Token));
        Assert.Equal(["OPS-3", "OPS-1", "OPS-2"], board.Columns.Single(column => column.Status.Id == backlog.Id).Issues.Select(issue => issue.Key));

        // Repeated moves into the same gap force a renumber; the order must stay exact.
        for (var round = 0; round < 12; round++)
        {
            var current = await ReadAsync<IssueDetailResponse>(await admin.GetAsync($"{Issues}/{two.Summary.Id}", Token));
            var anchor = round % 2 == 0 ? one.Summary.Id : three.Summary.Id;
            await ReadAsync<IssueDetailResponse>(await admin.PostAsJsonAsync(
                $"{Issues}/{two.Summary.Id}/move", new MoveIssueRequest(backlog.Id, false, null, anchor, current.Summary.Version), Token));
        }

        board = await ReadAsync<BoardResponse>(await admin.GetAsync($"{Projects}/{project.Id}/board", Token));
        Assert.Equal(["OPS-2", "OPS-3", "OPS-1"], board.Columns.Single(column => column.Status.Id == backlog.Id).Issues.Select(issue => issue.Key));

        // Hierarchy: subtasks need a standard parent; epics group standard issues; parents with children cannot be deleted.
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(admin.PostAsJsonAsync(Issues, IssueRequest(project.Id, "Subtask", "Orphan"), Token)));
        var epic = await CreateIssueAsync(admin, project.Id, "Epic", "Sample epic");
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(admin.PostAsJsonAsync(Issues, IssueRequest(project.Id, "Subtask", "Under epic") with { ParentId = epic.Summary.Id }, Token)));
        var child = await ReadAsync<IssueDetailResponse>(await admin.PostAsJsonAsync(Issues, IssueRequest(project.Id, "Subtask", "Child") with { ParentId = one.Summary.Id }, Token));
        Assert.Equal("OPS-1", child.ParentKey);
        var grouped = await ReadAsync<IssueDetailResponse>(await admin.PostAsJsonAsync(Issues, IssueRequest(project.Id, "Story", "Grouped") with { EpicId = epic.Summary.Id }, Token));
        Assert.Equal(epic.Summary.Key, grouped.Summary.EpicKey);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(admin.DeleteAsync($"{Issues}/{one.Summary.Id}", Token)));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(admin.DeleteAsync($"{Issues}/{child.Summary.Id}", Token)));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(admin.DeleteAsync($"{Issues}/{one.Summary.Id}", Token)));

        // Keys are never reused after a delete: six issues existed (OPS-1 and OPS-5 are now deleted), so the next is OPS-7.
        Assert.Equal("OPS-7", (await CreateIssueAsync(admin, project.Id, "Task", "After delete")).Summary.Key);
    }

    [Fact]
    public async Task Sprint_lifecycle_records_scope_and_moves_unfinished_work()
    {
        Fixture.RequireDatabase();
        using var admin = await SignInWithAsync("ws-admin", Permissions.Delivery.Access, Permissions.Delivery.Administer);
        using var developer = await SignInWithAsync("developer", Permissions.Delivery.Access);
        var project = await CreateProjectAsync(admin, "SAAS");
        project = await AddMemberAsync(admin, project, "developer", "Developer");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(developer.PostAsJsonAsync($"{Projects}/{project.Id}/sprints", new SaveSprintRequest("Sprint 1", null, today, today.AddDays(13), null), Token)));
        var sprint = await ReadAsync<SprintResponse>(await admin.PostAsJsonAsync($"{Projects}/{project.Id}/sprints", new SaveSprintRequest("Sprint 1", "Ship the sample", today, today.AddDays(13), null), Token));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(admin.PostAsJsonAsync($"{Delivery}/sprints/{sprint.Id}/start", new CloseSprintRequest(null, sprint.Version), Token)));

        var finished = await CreateIssueAsync(admin, project.Id, "Story", "Finished story", storyPoints: 5);
        var unfinished = await CreateIssueAsync(admin, project.Id, "Task", "Unfinished task", storyPoints: 3);
        foreach (var issue in new[] { finished, unfinished })
        {
            await ReadAsync<IssueDetailResponse>(await developer.PostAsJsonAsync(
                $"{Issues}/{issue.Summary.Id}/move", new MoveIssueRequest(null, true, sprint.Id, null, issue.Summary.Version), Token));
        }

        sprint = (await ReadAsync<IReadOnlyList<SprintResponse>>(await admin.GetAsync($"{Projects}/{project.Id}/sprints", Token))).Single();
        Assert.Equal(2, sprint.IssueCount);
        Assert.Equal(8, sprint.Points);
        var started = await ReadAsync<SprintResponse>(await admin.PostAsJsonAsync($"{Delivery}/sprints/{sprint.Id}/start", new CloseSprintRequest(null, sprint.Version), Token));
        Assert.Equal("Active", started.State);
        Assert.Equal(8, started.CommittedPoints);

        var second = await ReadAsync<SprintResponse>(await admin.PostAsJsonAsync($"{Projects}/{project.Id}/sprints", new SaveSprintRequest("Sprint 2", null, today.AddDays(14), today.AddDays(27), null), Token));
        await CreateIssueAsync(admin, project.Id, "Task", "Planned for sprint 2", sprintId: second.Id);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(admin.PostAsJsonAsync($"{Delivery}/sprints/{second.Id}/start", new CloseSprintRequest(null, second.Version), Token)));

        // Walk the finished story through the workflow to Done.
        var board = await ReadAsync<BoardResponse>(await developer.GetAsync($"{Projects}/{project.Id}/board", Token));
        Assert.Equal(started.Id, board.ActiveSprint!.Id);
        Assert.Equal(2, board.Columns.Sum(column => column.Issues.Count));
        // Moving the story into the sprint changed its version; continue from the current one.
        var current = await ReadAsync<IssueDetailResponse>(await developer.GetAsync($"{Issues}/{finished.Summary.Id}", Token));
        foreach (var key in new[] { "todo", "in-progress", "in-review", "testing", "done" })
        {
            current = await ReadAsync<IssueDetailResponse>(await developer.PostAsJsonAsync(
                $"{Issues}/{finished.Summary.Id}/transition", new TransitionIssueRequest(Status(project, key).Id, current.Summary.Version), Token));
        }

        Assert.Equal("Done", current.Summary.StatusCategory);
        Assert.NotNull(current.ResolvedAtUtc);

        var completed = await ReadAsync<SprintResponse>(await admin.PostAsJsonAsync($"{Delivery}/sprints/{started.Id}/complete", new CloseSprintRequest(null, started.Version), Token));
        Assert.Equal("Completed", completed.State);
        Assert.Equal(1, completed.CompletedIssueCount);
        Assert.Equal(5, completed.CompletedPoints);
        var moved = await ReadAsync<IssueDetailResponse>(await admin.GetAsync($"{Issues}/{unfinished.Summary.Id}", Token));
        Assert.Null(moved.Summary.SprintId);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(admin.PostAsJsonAsync($"{Delivery}/sprints/{completed.Id}/cancel", new CloseSprintRequest(null, completed.Version), Token)));
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(admin.PostAsJsonAsync(
            $"{Issues}/{unfinished.Summary.Id}/move", new MoveIssueRequest(null, true, completed.Id, null, moved.Summary.Version), Token)));

        var backlog = await ReadAsync<BacklogResponse>(await admin.GetAsync($"{Projects}/{project.Id}/backlog", Token));
        Assert.Contains(backlog.Backlog, issue => issue.Id == unfinished.Summary.Id);
        Assert.DoesNotContain(backlog.Backlog, issue => issue.Id == finished.Summary.Id);
        Assert.Single(backlog.Sprints);
    }

    [Fact]
    public async Task Search_my_work_and_dashboard_count_only_visible_real_data()
    {
        Fixture.RequireDatabase();
        using var admin = await SignInWithAsync("ws-admin", Permissions.Delivery.Access, Permissions.Delivery.Administer);
        using var developer = await SignInWithAsync("developer", Permissions.Delivery.Access);
        var visible = await CreateProjectAsync(admin, "AGT");
        var hidden = await CreateProjectAsync(admin, "HID");
        visible = await AddMemberAsync(admin, visible, "developer", "Developer");
        var developerId = await UserIdAsync("developer");
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        await ReadAsync<IssueDetailResponse>(await admin.PostAsJsonAsync(Issues, IssueRequest(visible.Id, "Bug", "Overdue sample bug") with
        {
            AssigneeId = developerId,
            DueDate = yesterday,
            Labels = ["Agents", "urgent"],
        }, Token));
        await CreateIssueAsync(admin, visible.Id, "Task", "Unassigned sample task");
        await CreateIssueAsync(admin, hidden.Id, "Task", "Hidden sample task");
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(admin.PostAsJsonAsync(Issues, IssueRequest(hidden.Id, "Task", "Bad assignee") with { AssigneeId = developerId }, Token)));

        var mine = await ReadAsync<PagedResult<IssueListItemResponse>>(await developer.GetAsync($"{Issues}?mine=true", Token));
        var item = Assert.Single(mine.Items);
        Assert.True(item.IsOverdue);
        Assert.Equal(["agents", "urgent"], item.Labels);
        Assert.Equal(1, (await ReadAsync<PagedResult<IssueListItemResponse>>(await developer.GetAsync($"{Issues}?label=AGENTS", Token))).TotalCount);
        Assert.Equal(1, (await ReadAsync<PagedResult<IssueListItemResponse>>(await developer.GetAsync($"{Issues}?text=overdue%20sample", Token))).TotalCount);
        Assert.Equal(2, (await ReadAsync<PagedResult<IssueListItemResponse>>(await developer.GetAsync(Issues, Token))).TotalCount);
        Assert.Equal(3, (await ReadAsync<PagedResult<IssueListItemResponse>>(await admin.GetAsync(Issues, Token))).TotalCount);

        var dashboard = await ReadAsync<DeliveryDashboardResponse>(await developer.GetAsync($"{Delivery}/dashboard", Token));
        Assert.Equal(1, dashboard.ActiveProjects);
        Assert.Equal(2, dashboard.OpenIssues);
        Assert.Equal(1, dashboard.AssignedToMe);
        Assert.Equal(1, dashboard.Overdue);
        Assert.Equal(0, dashboard.CompletedThisWeek);
        Assert.Single(dashboard.Workload);
        Assert.All(dashboard.RecentlyUpdated, issue => Assert.Equal("AGT", issue.ProjectKey));
        Assert.All(dashboard.RecentActivity, activity => Assert.StartsWith("AGT-", activity.IssueKey, StringComparison.Ordinal));

        // The last project Admin cannot be demoted or removed.
        var adminId = await UserIdAsync("ws-admin");
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(admin.PostAsJsonAsync($"{Projects}/{visible.Id}/members", new SaveProjectMemberRequest(adminId, "Developer", visible.Version), Token)));
        var historyRows = await QueryAsync(db => db.DeliveryIssueHistory.Where(entry => entry.ProjectId == visible.Id));
        Assert.All(historyRows, entry => Assert.Equal(visible.Id, entry.ProjectId));
    }

    private static SaveDeliveryProjectRequest ProjectRequest(string key) =>
        new(key, $"Sample {key} project", "Fictional project for tests.", "AiAgent", "Internal", null, "Active", "High", null, null, null, null);

    private static SaveIssueRequest IssueRequest(Guid projectId, string type, string title, int? storyPoints = null, Guid? sprintId = null) =>
        new(projectId, type, title, null, "Medium", null, storyPoints, null, null, null, null, null, null, null, sprintId, null, null, null, null, null);

    private static async Task<DeliveryProjectResponse> CreateProjectAsync(HttpClient client, string key) =>
        await ReadAsync<DeliveryProjectResponse>(await client.PostAsJsonAsync(Projects, ProjectRequest(key), Token));

    private static async Task<IssueDetailResponse> CreateIssueAsync(HttpClient client, Guid projectId, string type, string title, int? storyPoints = null, Guid? sprintId = null) =>
        await ReadAsync<IssueDetailResponse>(await client.PostAsJsonAsync(Issues, IssueRequest(projectId, type, title, storyPoints, sprintId), Token));

    private static WorkflowStatusResponse Status(DeliveryProjectResponse project, string key) =>
        project.Statuses.Single(status => status.SystemKey == key);

    private async Task<DeliveryProjectResponse> AddMemberAsync(HttpClient admin, DeliveryProjectResponse project, string name, string role) =>
        await ReadAsync<DeliveryProjectResponse>(await admin.PostAsJsonAsync(
            $"{Projects}/{project.Id}/members", new SaveProjectMemberRequest(await UserIdAsync(name), role, project.Version), Token));

    private async Task<Guid> UserIdAsync(string name) =>
        (await QueryAsync(db => db.Users.Where(user => user.Email == $"{name}@onyx.test").Select(user => user.Id))).Single();
}
