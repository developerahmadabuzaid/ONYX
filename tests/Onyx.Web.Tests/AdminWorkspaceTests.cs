using System.Net;
using System.Text.Json;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>
/// The redesigned workspace against a fake API: figures and names come from the API and are encoded, the theme preference is
/// honoured, calendar moves and bulk changes are forwarded as entered, attachments are proxied as downloads, and only real
/// comparisons are shown. All data is fictional sample data.
/// </summary>
public sealed class AdminWorkspaceTests : IAsyncLifetime
{
    private static readonly Guid ProjectId = Guid.CreateVersion7();
    private static readonly Guid IssueId = Guid.CreateVersion7();
    private static readonly Guid SprintId = Guid.CreateVersion7();
    private static readonly Guid PersonId = Guid.CreateVersion7();
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
    public async Task The_dashboard_shows_real_figures_encoded_and_only_real_changes()
    {
        await SignInAsync(Permissions.Delivery.Access);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/delivery/analytics", _ => FakeOnyxApi.Json(Analytics(completedNow: 6, completedBefore: 3, createdBefore: 0)));
        var html = await _browser.GetHtmlAsync("/admin/delivery?range=7");

        Assert.Contains("range=7", _factory.Api.RequestsTo("api/v1/admin/delivery/analytics").Last().Query, StringComparison.Ordinal);
        Assert.Contains("class=\"trend trend--good\"", html, StringComparison.Ordinal);
        Assert.Contains("100%", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-widgets hidden", html, StringComparison.Ordinal);
        Assert.Contains("class=\"lchart chart\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"donut chart\"", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain(Hostile, html, StringComparison.Ordinal);

        // Created had nothing to compare with (previous period 0): no percentage is invented for it.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "class=\"trend trend--"));
    }

    [Fact]
    public async Task The_theme_preference_is_applied_on_the_server()
    {
        await SignInAsync(Permissions.Delivery.Access);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/delivery/analytics", _ => FakeOnyxApi.Json(Analytics(1, 1, 1)));

        var plain = await _browser.GetHtmlAsync("/admin/delivery");
        Assert.Contains("data-surface=\"admin\"", plain, StringComparison.Ordinal);
        Assert.Contains("data-theme=\"system\"", plain, StringComparison.Ordinal);

        foreach (var (value, expected) in new[] { ("dark", "data-theme=\"dark\""), ("light", "data-theme=\"light\""), ("<b>", "data-theme=\"system\"") })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/delivery");
            request.Headers.Add("Cookie", $"onyx-admin-theme={Uri.EscapeDataString(value)}");
            using var response = await _browser.SendAsync(request, Token);
            var html = await response.Content.ReadAsStringAsync(Token);
            Assert.Contains(expected, html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_calendar_asks_for_the_visible_period_and_forwards_moves()
    {
        await SignInAsync(Permissions.Delivery.Access);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/delivery/issues", _ => FakeOnyxApi.Json(new PagedResult<IssueListItemResponse>(
            [Item(IssueId, "SAMP-1") with { DueDate = new DateOnly(2026, 11, 10), StartDate = new DateOnly(2026, 11, 8) }], 1, 100, 1)));
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/delivery/projects", _ => FakeOnyxApi.Json(Array.Empty<DeliveryProjectListItemResponse>()));
        var html = await _browser.GetHtmlAsync("/admin/delivery/calendar?date=2026-11-15");

        var query = _factory.Api.RequestsTo("api/v1/admin/delivery/issues").Last().Query;
        Assert.Contains("dueFrom=2026-10-26", query, StringComparison.Ordinal);
        Assert.Contains("dueTo=2026-12-06", query, StringComparison.Ordinal);
        Assert.Contains("data-day=\"2026-11-10\"", html, StringComparison.Ordinal);
        Assert.Contains("data-start=\"2026-11-08\"", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);

        _factory.Api.On(HttpMethod.Post, $"api/v1/admin/delivery/issues/{IssueId:D}/dates", _ => FakeOnyxApi.Json(Detail() with { }));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/delivery/issues/{IssueId:D}/dates")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["dueDate"] = "2026-11-12",
                ["startDate"] = "2026-11-10",
                ["version"] = "41",
                ["__RequestVerificationToken"] = BrowserExtensions.AntiforgeryToken(html),
            }),
        };
        request.Headers.Add("Accept", "application/json");
        using var moved = await _browser.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        using var json = JsonDocument.Parse(await moved.Content.ReadAsStringAsync(Token));
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        var sent = _factory.Api.RequestsTo($"api/v1/admin/delivery/issues/{IssueId:D}/dates").Last().BodyAs<RescheduleIssueRequest>();
        Assert.Equal(new RescheduleIssueRequest(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12), 41), sent);
    }

    [Fact]
    public async Task Bulk_changes_are_forwarded_and_refusals_are_reported()
    {
        await SignInAsync(Permissions.Delivery.Access);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/delivery/issues", _ => FakeOnyxApi.Json(new PagedResult<IssueListItemResponse>([Item(IssueId, "SAMP-1")], 1, 25, 1)));
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/delivery/projects", _ => FakeOnyxApi.Json(Array.Empty<DeliveryProjectListItemResponse>()));
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/delivery/filters", _ => FakeOnyxApi.Json(Array.Empty<SavedIssueFilterResponse>()));
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/delivery/projects/users", _ => FakeOnyxApi.Json(new[] { new DeliveryUserResponse(PersonId, "Sample Person") }));
        var other = Guid.CreateVersion7();
        _factory.Api.On(HttpMethod.Post, "api/v1/admin/delivery/issues/bulk", _ => FakeOnyxApi.Json(
            new BulkIssueResultResponse(1, [new BulkIssueFailureResponse(other, "RDO-4", "Delivery.Forbidden", "Not allowed.")])));

        var page = await _browser.GetHtmlAsync("/admin/delivery/issues?type=Bug");
        Assert.Contains("data-bulk-form", page, StringComparison.Ordinal);
        Assert.Contains("name=\"query\" value=\"type=Bug\"", page, StringComparison.Ordinal);

        using var content = new FormUrlEncodedContent(
        [
            new("issueIds", IssueId.ToString("D")),
            new("issueIds", other.ToString("D")),
            new("bulkAction", "Assign"),
            new("assigneeId", PersonId.ToString("D")),
            new("returnUrl", "/admin/delivery/issues?type=Bug"),
            new("__RequestVerificationToken", BrowserExtensions.AntiforgeryToken(page)),
        ]);
        using var response = await _browser.PostAsync("/admin/delivery/issues/bulk", content, Token);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/delivery/issues?type=Bug", response.Location());
        var sent = _factory.Api.RequestsTo("api/v1/admin/delivery/issues/bulk").Last().BodyAs<BulkIssueRequest>();
        Assert.Equal([IssueId, other], sent.IssueIds);
        Assert.Equal("Assign", sent.Action);
        Assert.Equal(PersonId, sent.AssigneeId);

        var after = await _browser.GetHtmlAsync("/admin/delivery/issues?type=Bug");
        Assert.Contains("RDO-4", after, StringComparison.Ordinal);

        // A return address outside the Admin is never followed.
        using var outside = new FormUrlEncodedContent(
        [
            new("issueIds", IssueId.ToString("D")),
            new("bulkAction", "Assign"),
            new("returnUrl", "https://example.test/elsewhere"),
            new("__RequestVerificationToken", BrowserExtensions.AntiforgeryToken(after)),
        ]);
        using var safe = await _browser.PostAsync("/admin/delivery/issues/bulk", outside, Token);
        Assert.Equal("/admin/delivery/issues", safe.Location());
    }

    [Fact]
    public async Task Attachments_are_proxied_as_downloads_and_images_may_be_shown_inline()
    {
        await SignInAsync(Permissions.Delivery.Access);
        var attachmentId = Guid.CreateVersion7();
        var path = $"api/v1/admin/delivery/issues/{IssueId:D}/attachments/{attachmentId:D}";
        _factory.Api.On(HttpMethod.Get, path, _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = "screen.png" };
            return response;
        });

        using var download = await _browser.GetAsync($"/admin/delivery/issues/{IssueId:D}/attachments/{attachmentId:D}", Token);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("no-store", download.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);

        using var inline = await _browser.GetAsync($"/admin/delivery/issues/{IssueId:D}/attachments/{attachmentId:D}?inline=true", Token);
        Assert.Null(inline.Content.Headers.ContentDisposition);
        Assert.Equal("image/png", inline.Content.Headers.ContentType?.MediaType);

        using var missing = await _browser.GetAsync($"/admin/delivery/issues/{IssueId:D}/attachments/{Guid.CreateVersion7():D}", Token);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task The_sprint_report_offers_the_retrospective_only_to_sprint_managers()
    {
        await SignInAsync(Permissions.Delivery.Access);
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/delivery/sprints/{SprintId:D}/report", _ => FakeOnyxApi.Json(Report(manage: false)));
        var viewer = await _browser.GetHtmlAsync($"/admin/delivery/sprints/{SprintId:D}");
        Assert.Contains("class=\"lchart chart\"", viewer, StringComparison.Ordinal);
        Assert.DoesNotContain($"/admin/delivery/sprints/{SprintId:D}/retrospective", viewer, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", viewer, StringComparison.Ordinal);
        Assert.DoesNotContain(Hostile, viewer, StringComparison.Ordinal);

        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/delivery/sprints/{SprintId:D}/report", _ => FakeOnyxApi.Json(Report(manage: true)));
        _factory.Api.On(HttpMethod.Put, $"api/v1/admin/delivery/sprints/{SprintId:D}/retrospective", _ => FakeOnyxApi.Json(Report(manage: true)));
        var manager = await _browser.GetHtmlAsync($"/admin/delivery/sprints/{SprintId:D}");
        using var saved = await _browser.PostFormAsync($"/admin/delivery/sprints/{SprintId:D}/retrospective", new Dictionary<string, string>
        {
            ["wentWell"] = "Pairing on samples",
            ["toImprove"] = "Smaller stories",
            ["actionItems"] = string.Empty,
            ["version"] = "9",
        }, BrowserExtensions.AntiforgeryToken(manager));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var sent = _factory.Api.RequestsTo($"api/v1/admin/delivery/sprints/{SprintId:D}/retrospective").Last().BodyAs<SaveRetrospectiveRequest>();
        Assert.Equal("Smaller stories", sent.ToImprove);
        Assert.Equal(9u, sent.Version);
    }

    [Fact]
    public async Task The_new_menu_entries_follow_the_workspace_permission()
    {
        await SignInAsync(Permissions.Delivery.Access);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/delivery/roadmap", _ => FakeOnyxApi.Json(new RoadmapResponse([])));
        var html = await _browser.GetHtmlAsync("/admin/delivery/roadmap");
        foreach (var href in new[] { "/admin/delivery/sprints", "/admin/delivery/roadmap", "/admin/delivery/calendar", "/admin/delivery/reports" })
        {
            Assert.Contains($"href=\"{href}\"", html, StringComparison.Ordinal);
        }

        Assert.Contains("data-palette", html, StringComparison.Ordinal);
        Assert.Contains("/admin/delivery/issues/new", html, StringComparison.Ordinal);
    }

    private async Task SignInAsync(params string[] permissions)
    {
        AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", permissions));
        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    private static IssueListItemResponse Item(Guid id, string key) =>
        new(id, key, ProjectId, "SAMP", "Task", $"Sample {Hostile} task", Guid.CreateVersion7(), "To Do", "todo", "ToDo", "High", null, null, 3, null, [],
            null, null, null, null, null, 1024, false, DateTimeOffset.UtcNow, 41);

    private static IssueDetailResponse Detail() =>
        new(Item(IssueId, "SAMP-1"), null, "Sample Reporter", Guid.CreateVersion7(), null, null, [], null, null, null, null, null, null, null,
            DateTimeOffset.UtcNow, null, [], [], [], [], [], 1, false, new DeliveryCapabilities(true, true, true, true, true, true));

    private static DeliveryAnalyticsResponse Analytics(int completedNow, int completedBefore, int createdBefore)
    {
        var today = new DateOnly(2026, 11, 25);
        var timeline = Enumerable.Range(0, 7).Select(offset => new DeliveryTrendPointResponse(today.AddDays(offset - 6), today.AddDays(offset - 6), offset, offset % 2)).ToList();
        return new DeliveryAnalyticsResponse(
            7, today.AddDays(-6), today, "day",
            new DeliveryKpisResponse(2, 1, 2, 12, 5, createdBefore, 4, completedNow, completedBefore, 5, 2, 1, 3),
            timeline,
            new StatusBreakdownResponse(4, 2, 5, 1),
            [new ProjectHealthResponse(ProjectId, "SAMP", $"Sample {Hostile} project", "Active", "AtRisk", "High", 12, 4, 1, 3, null, null, DateTimeOffset.UtcNow)],
            [new TeamMemberStatsResponse(PersonId, $"Sample {Hostile} person", 2, 1, 1, 5, 3)],
            [],
            [Item(IssueId, "SAMP-1")],
            [],
            [],
            []);
    }

    private static SprintReportResponse Report(bool manage)
    {
        var start = new DateOnly(2026, 11, 16);
        var sprint = new SprintResponse(SprintId, $"Sprint {Hostile}", "Sample goal", start, start.AddDays(13), "Active", 2, 8, 1, 5, 2, 8, null, null,
            DateTimeOffset.UtcNow.AddDays(-9), null, 9);
        return new SprintReportResponse(ProjectId, "SAMP", "Sample project", sprint,
            [new BurndownPointResponse(start, 8, 2), new BurndownPointResponse(start.AddDays(1), 3, 1)],
            [Item(IssueId, "SAMP-1")],
            new SprintRetrospectiveResponse(null, null, null, null, null),
            new DeliveryCapabilities(true, true, true, true, manage, manage));
    }
}
