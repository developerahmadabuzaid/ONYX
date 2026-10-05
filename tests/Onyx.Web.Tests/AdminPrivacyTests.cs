using System.Net;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Privacy;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>Admin privacy screens: permission-filtered menu, export streamed without caching, erasure confirmation passed to the API.</summary>
public sealed class AdminPrivacyTests : IAsyncLifetime
{
    private static readonly Guid RequestId = Guid.CreateVersion7();

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
    public async Task Privacy_screens_appear_only_with_privacy_manage()
    {
        await SignInAsync(Permissions.Leads.View);
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/notifications", _ => FakeOnyxApi.Json(new PagedResult<Onyx.Contracts.Notifications.AdminNotificationResponse>([], 1, 20, 0)));

        var html = await _browser.GetHtmlAsync("/admin/notifications");

        Assert.DoesNotContain("href=\"/admin/privacy/requests\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/privacy/retention\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_export_is_streamed_without_caching_and_erasure_sends_the_typed_confirmation()
    {
        await SignInAsync(Permissions.Privacy.Manage);
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/data-subject-requests/{RequestId:D}", _ => FakeOnyxApi.Json(Request("Erasure", "Verified")));
        _factory.Api.On(HttpMethod.Post, $"api/v1/admin/data-subject-requests/{RequestId:D}/export", _ => FakeOnyxApi.Json(
            new DataSubjectExportFile("onyx-data-export.json", "{\"subject\":{\"email\":\"subject@example.com\"},\"name\":\"سارة\"}")));
        _factory.Api.On(HttpMethod.Post, $"api/v1/admin/data-subject-requests/{RequestId:D}/erase", _ => FakeOnyxApi.Json(Request("Erasure", "Completed")));

        var page = await _browser.GetHtmlAsync($"/admin/privacy/requests/{RequestId:D}");
        Assert.Contains("href=\"/admin/privacy/requests\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmSubjectEmail\"", page, StringComparison.Ordinal);

        using var export = await _browser.PostFormAsync($"/admin/privacy/requests/{RequestId:D}/export", new Dictionary<string, string>(), BrowserExtensions.AntiforgeryToken(page));
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Contains("no-store", export.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Equal("attachment", export.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Contains("سارة", await export.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        using var erase = await _browser.PostFormAsync($"/admin/privacy/requests/{RequestId:D}/erase", new Dictionary<string, string>
        {
            ["confirmSubjectEmail"] = "subject@example.com",
        }, BrowserExtensions.AntiforgeryToken(page));
        Assert.Equal(HttpStatusCode.Redirect, erase.StatusCode);
        Assert.Contains("\"confirmSubjectEmail\":\"subject@example.com\"", Assert.Single(_factory.Api.RequestsTo($"api/v1/admin/data-subject-requests/{RequestId:D}/erase")).Body, StringComparison.Ordinal);

        // Without the antiforgery token the destructive post never reaches the API.
        using var forged = await _browser.PostFormAsync($"/admin/privacy/requests/{RequestId:D}/erase", new Dictionary<string, string> { ["confirmSubjectEmail"] = "x@example.com" }, null);
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Single(_factory.Api.RequestsTo($"api/v1/admin/data-subject-requests/{RequestId:D}/erase"));
    }

    private async Task SignInAsync(params string[] permissions)
    {
        AdminApiStubs.SignInSucceeds(_factory, AdminApiStubs.User("en", permissions));
        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    private DataSubjectRequestResponse Request(string type, string status) =>
        new(RequestId, type, status, status == "Completed" ? null : "subject@example.com", "0a1b2c3d4e5f", null, _factory.Time.GetUtcNow(), _factory.Time.GetUtcNow(),
            "Checked by reply.", status == "Completed" ? _factory.Time.GetUtcNow() : null, null, null, 3);
}
