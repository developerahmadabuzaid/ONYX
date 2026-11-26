using System.Net;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Identity;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>
/// Staff and role screens against a fake API: the menu follows Users.Manage / Roles.Manage, invitations and role changes are
/// forwarded as entered, nobody is offered actions against their own account, and SuperAdmin cannot be edited. Fictional data.
/// </summary>
public sealed class AdminStaffTests : IAsyncLifetime
{
    private static readonly Guid OtherId = Guid.CreateVersion7();
    private static readonly Guid RoleId = Guid.CreateVersion7();
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
    public async Task The_team_menu_follows_the_permissions()
    {
        await SignInAsync(Permissions.Users.Manage);
        StubUsers();
        var html = await _browser.GetHtmlAsync("/admin/staff/users");
        Assert.Contains("href=\"/admin/staff/users\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/staff/roles\"", html, StringComparison.Ordinal);

        // Without Roles.Manage the built-in roles are still offered for invitations.
        Assert.Contains("name=\"Roles\" value=\"Sales\"", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain(Hostile, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invitations_are_forwarded_with_the_chosen_roles()
    {
        await SignInAsync(Permissions.Users.Manage, Permissions.Roles.Manage);
        StubUsers();
        StubRoles();
        _factory.Api.On(HttpMethod.Post, "api/v1/admin/users", _ => FakeOnyxApi.Json(Person(OtherId, "Sample Person"), HttpStatusCode.Created));

        using var response = await _browser.SubmitFormAsync("/admin/staff/users", "/admin/staff/users", new Dictionary<string, string>
        {
            ["DisplayName"] = "Sample Person",
            ["Email"] = "sample.person@example.test",
            ["Language"] = "en",
            ["Roles"] = "Sales",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/admin/staff/users/{OtherId:D}", response.Location());
        var sent = _factory.Api.RequestsTo("api/v1/admin/users").Last(request => request.Method == HttpMethod.Post).BodyAs<InviteUserRequest>();
        Assert.Equal("sample.person@example.test", sent.Email);
        Assert.Equal(["Sales"], sent.Roles);
        Assert.Equal("en", sent.PreferredLanguage);
    }

    [Fact]
    public async Task Nobody_is_offered_disable_or_two_factor_reset_on_their_own_account()
    {
        var me = await SignInAsync(Permissions.Users.Manage);
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/users/{me.Id:D}", _ => FakeOnyxApi.Json(Person(me.Id, "Sample Owner", twoFactor: true)));
        _factory.Api.On(HttpMethod.Get, $"api/v1/admin/users/{OtherId:D}", _ => FakeOnyxApi.Json(Person(OtherId, "Sample Person", twoFactor: true)));

        var mine = await _browser.GetHtmlAsync($"/admin/staff/users/{me.Id:D}");
        Assert.DoesNotContain($"/admin/staff/users/{me.Id:D}/disable", mine, StringComparison.Ordinal);
        Assert.DoesNotContain($"/admin/staff/users/{me.Id:D}/reset-two-factor", mine, StringComparison.Ordinal);

        var theirs = await _browser.GetHtmlAsync($"/admin/staff/users/{OtherId:D}");
        Assert.Contains($"/admin/staff/users/{OtherId:D}/disable", theirs, StringComparison.Ordinal);
        Assert.Contains($"/admin/staff/users/{OtherId:D}/reset-two-factor", theirs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Role_changes_are_forwarded_and_superadmin_is_read_only()
    {
        await SignInAsync(Permissions.Roles.Manage);
        StubRoles();
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/permissions", _ => FakeOnyxApi.Json(new[]
        {
            new PermissionResponse(Permissions.Leads.View, "Leads"),
            new PermissionResponse(Permissions.Products.Publish, "Products"),
        }));
        _factory.Api.On(HttpMethod.Put, $"api/v1/admin/roles/{RoleId:D}", _ => FakeOnyxApi.Json(Role(RoleId, "Sales", [Permissions.Leads.View])));

        var page = await _browser.GetHtmlAsync($"/admin/staff/roles/{RoleId:D}");
        Assert.Contains("value=\"Leads.View\" checked", page, StringComparison.Ordinal);
        using var saved = await _browser.PostFormAsync($"/admin/staff/roles/{RoleId:D}", new Dictionary<string, string>
        {
            ["name"] = "Sales",
            ["permissions"] = Permissions.Products.Publish,
        }, BrowserExtensions.AntiforgeryToken(page));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var sent = _factory.Api.RequestsTo($"api/v1/admin/roles/{RoleId:D}").Last().BodyAs<SaveRoleRequest>();
        Assert.Equal([Permissions.Products.Publish], sent.Permissions);

        var superAdmin = await _browser.GetHtmlAsync($"/admin/staff/roles/{SuperAdminId:D}");
        Assert.DoesNotContain("type=\"submit\" class=\"btn btn--primary\"", superAdmin, StringComparison.Ordinal);
    }

    private static readonly Guid SuperAdminId = Guid.CreateVersion7();

    private async Task<CurrentUserResponse> SignInAsync(params string[] permissions)
    {
        var user = AdminApiStubs.User("en", permissions);
        AdminApiStubs.SignInSucceeds(_factory, user);
        using var login = await AdminApiStubs.LogInAsync(_browser);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return user;
    }

    private void StubUsers() =>
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/users", _ => FakeOnyxApi.Json(
            new PagedResult<UserSummaryResponse>([Person(OtherId, $"Sample {Hostile}")], 1, 25, 1)));

    private void StubRoles() =>
        _factory.Api.On(HttpMethod.Get, "api/v1/admin/roles", _ => FakeOnyxApi.Json(new[]
        {
            Role(SuperAdminId, "SuperAdmin", [], isSystem: true),
            Role(RoleId, "Sales", [Permissions.Leads.View], isSystem: true),
        }));

    private static UserSummaryResponse Person(Guid id, string name, bool twoFactor = false) =>
        new(id, "sample.person@example.test", name, "en", true, true, twoFactor, false, ["Sales"], DateTimeOffset.UtcNow, null);

    private static RoleResponse Role(Guid id, string name, IReadOnlyList<string> permissions, bool isSystem = false) => new(id, name, isSystem, permissions, 1);
}
