using System.Net;
using System.Text.Json;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Delivery;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>
/// Delivery workspace screens against a fake API: the menu follows the permission, the board and issue pages render API data
/// encoded and only offer what the caller's capabilities allow, and drag-and-drop moves are forwarded with their version.
/// All data is fictional sample data.
/// </summary>
public sealed class AdminDeliveryTests : IAsyncLifetime
{
    private static readonly Guid ProjectId = Guid.CreateVersion7();
    private static readonly Guid IssueId = Guid.CreateVersion7();
    private static readonly Guid BacklogId = Guid.CreateVersion7();
    private static readonly Guid TodoId = Guid.CreateVersion7();
    private static readonly Guid DoneId = Guid.CreateVersion7();
    private const string Hostile = "<script>alert('x')</script>";

    private readonly OnyxWebFactory _factory = new();
    private HttpClient _browser = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _browser = _factory.CreateBrowser();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _browser.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task The_delivery_menu_is_hidden_without_workspace_access()
    {
        await SignInAsync(Permissions.News.View);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/news-categories", _ => FakeOnyxApi.Json(Array.Empty<object>()));
        var html = await _browser.GetHtmlAsync("/admin/news/categories");
        Assert.DoesNotContain("href=\"/admin/delivery\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_delivery_menu_appears_with_workspace_access()
    {
        await SignInAsync(Permissions.Delivery.Access);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/delivery/analytics", _ => FakeOnyxApi.Json(EmptyAnalytics()));
        var html = await _browser.GetHtmlAsync("/admin/delivery");
        Assert.Contains("href=\"/admin/delivery/projects\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/delivery/my-work\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/delivery/issues\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_board_renders_columns_encoded_and_is_draggable_only_for_editors()
    {
        await SignInAsync(Permissions.Delivery.Access);
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/delivery/projects/{ProjectId:D}/board", _ => FakeOnyxApi.Json(Board(Capabilities(edit: true))));

        var html = await _browser.GetHtmlAsync($"/admin/delivery/projects/{ProjectId:D}/board");

        Assert.Contains("data-board=\"editable\"", html, StringComparison.Ordinal);
        Assert.Contains($"data-status-id=\"{TodoId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains($"data-issue-id=\"{IssueId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains($"{BacklogId:D}&gt;{TodoId:D}", html, StringComparison.Ordinal);
        Assert.Contains("SAMP-1", html, StringComparison.Ordinal);
        Assert.DoesNotContain(Hostile, html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("is-over", html, StringComparison.Ordinal);

        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/delivery/projects/{ProjectId:D}/board", _ => FakeOnyxApi.Json(Board(Capabilities(edit: false))));
        var readOnly = await _browser.GetHtmlAsync($"/admin/delivery/projects/{ProjectId:D}/board");
        Assert.Contains("data-board=\"readonly\"", readOnly, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_issue_page_encodes_text_and_hides_actions_the_role_cannot_take()
    {
        await SignInAsync(Permissions.Delivery.Access);
        StubIssuePage(Capabilities(edit: false));

        var html = await _browser.GetHtmlAsync($"/admin/delivery/issues/{IssueId:D}");

        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain(Hostile, html, StringComparison.Ordinal);
        Assert.DoesNotContain($"/admin/delivery/issues/{IssueId:D}/transition", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"/admin/delivery/issues/{IssueId:D}/delete", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"action=\"/admin/delivery/issues/{IssueId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains($"/admin/delivery/issues/{IssueId:D}/comments", html, StringComparison.Ordinal);

        StubIssuePage(Capabilities(edit: true));
        var editable = await _browser.GetHtmlAsync($"/admin/delivery/issues/{IssueId:D}");
        Assert.Contains($"/admin/delivery/issues/{IssueId:D}/transition", editable, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Board_moves_are_forwarded_with_their_version_and_refusals_come_back_as_json()
    {
        await SignInAsync(Permissions.Delivery.Access);
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/delivery/projects/{ProjectId:D}/board", _ => FakeOnyxApi.Json(Board(Capabilities(edit: true))));
        _factory.Api.On(HttpMethod.Post, $"api/v1/admin/delivery/issues/{IssueId:D}/move", _ => FakeOnyxApi.Json(Detail(Capabilities(edit: true)) with { }));
        var page = await _browser.GetHtmlAsync($"/admin/delivery/projects/{ProjectId:D}/board");
        var token = BrowserExtensions.AntiforgeryToken(page);
        var fields = new Dictionary<string, string> { ["statusId"] = TodoId.ToString("D"), ["beforeIssueId"] = string.Empty, ["version"] = "41" };

        using (var moved = await _browser.PostFormAsync($"/admin/delivery/issues/{IssueId:D}/move", fields, token))
        {
            Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
            using var json = JsonDocument.Parse(await moved.Content.ReadAsStringAsync(Token));
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        }

        var sent = _factory.Api.RequestsTo($"api/v1/admin/delivery/issues/{IssueId:D}/move").Last();
        Assert.Contains("\"version\":41", sent.Body, StringComparison.Ordinal);
        Assert.Contains(TodoId.ToString("D"), sent.Body, StringComparison.Ordinal);

        _factory.Api.On(HttpMethod.Post, $"api/v1/admin/delivery/issues/{IssueId:D}/move", _ => FakeOnyxApi.Problem(HttpStatusCode.Conflict, "Delivery.TransitionNotAllowed"));
        using var refused = await _browser.PostFormAsync($"/admin/delivery/issues/{IssueId:D}/move", fields, token);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token));
        Assert.False(problem.RootElement.GetProperty("ok").GetBoolean());
    }

    private async Task SignInAsync(params string[] permissions)
    {
        AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", permissions));
        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    private void StubIssuePage(DeliveryCapabilities can)
    {
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/delivery/issues/{IssueId:D}", _ => FakeOnyxApi.Json(Detail(can)));
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/delivery/projects/{ProjectId:D}", _ => FakeOnyxApi.Json(Project(can)));
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/delivery/issues", _ => FakeOnyxApi.Json(new Onyx.Contracts.Common.PagedResult<IssueListItemResponse>([], 1, 100, 0)));
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/delivery/projects/{ProjectId:D}/sprints", _ => FakeOnyxApi.Json(Array.Empty<SprintResponse>()));
    }

    private static DeliveryCapabilities Capabilities(bool edit) => new(true, true, true, edit, edit, edit);

    private static WorkflowStatusResponse[] Statuses() =>
    [
        new(BacklogId, "Backlog", "backlog", "ToDo", 0, null),
        new(TodoId, "To Do", "todo", "ToDo", 1, 1),
        new(DoneId, "Done", "done", "Done", 2, null),
    ];

    private static IssueListItemResponse Item(Guid id, string key, Guid statusId) =>
        new(id, key, ProjectId, "SAMP", "Task", $"Sample {Hostile} card", statusId, "To Do", "todo", "ToDo", "High", null, null, 3, null, ["sample"],
            null, null, null, null, null, 1024, false, DateTimeOffset.UtcNow, 41);

    private static BoardResponse Board(DeliveryCapabilities can) =>
        new(ProjectId, "SAMP", "Sample project", null,
            [
                new(Statuses()[0], []),
                new(Statuses()[1], [Item(IssueId, "SAMP-1", TodoId), Item(Guid.CreateVersion7(), "SAMP-2", TodoId)]),
                new(Statuses()[2], []),
            ],
            [new(BacklogId, TodoId), new(TodoId, DoneId)],
            [],
            can,
            7);

    private static DeliveryProjectResponse Project(DeliveryCapabilities can) =>
        new(ProjectId, "SAMP", "Sample project", null, "Software", "Internal", null, "Active", "Medium", Guid.CreateVersion7(), "Sample Owner", null, null, null,
            "Developer", can, Statuses(), [new(BacklogId, TodoId)], [], 2, 0, 7);

    private static IssueDetailResponse Detail(DeliveryCapabilities can) =>
        new(Item(IssueId, "SAMP-1", BacklogId) with { StatusId = BacklogId, StatusKey = "backlog" }, $"Steps: {Hostile}", "Sample Reporter", Guid.CreateVersion7(),
            null, null, [], null, null, null, null, null, null, null, DateTimeOffset.UtcNow, null, [], [], [], [], [Statuses()[1]], 1, true, can);

    private static DeliveryAnalyticsResponse EmptyAnalytics() =>
        new(30, new DateOnly(2026, 10, 27), new DateOnly(2026, 11, 25), "day", new DeliveryKpisResponse(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), [],
            new StatusBreakdownResponse(0, 0, 0, 0), [], [], [], [], [], [], []);
}
