using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.Authorization;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Authorization;

namespace Onyx.API.IntegrationTests.Identity;

public sealed class AuthorizationTests(PostgresFixture fixture) : IdentityApiTestBase(fixture)
{
    public static TheoryData<string, string> AdminEndpoints => new()
    {
        { "GET", "/api/v1/admin/users" },
        { "POST", "/api/v1/admin/users" },
        { "GET", $"/api/v1/admin/users/{Guid.Empty}" },
        { "PUT", $"/api/v1/admin/users/{Guid.Empty}/roles" },
        { "POST", $"/api/v1/admin/users/{Guid.Empty}/disable" },
        { "POST", $"/api/v1/admin/users/{Guid.Empty}/two-factor/reset" },
        { "GET", "/api/v1/admin/roles" },
        { "POST", "/api/v1/admin/roles" },
        { "DELETE", $"/api/v1/admin/roles/{Guid.Empty}" },
        { "GET", "/api/v1/admin/permissions" },
        { "GET", "/api/v1/admin/audit-logs" },
        { "GET", "/api/v1/admin/provider-connections" },
        { "POST", "/api/v1/admin/provider-connections/email.smtp/disable" },
        { "POST", "/api/v1/admin/provider-connections/email/test" },
    };

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Admin_endpoints_reject_anonymous_callers_with_401(string method, string path)
    {
        Fixture.RequireDatabase();

        using var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) }, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Admin_endpoints_reject_authenticated_users_without_the_permission_with_403(string method, string path)
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        using var sales = AuthorizedClient((await SignInAsync("sales@onyx.test", StrongPassword)).AccessToken);

        using var response = await sales.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Notifications_require_authentication_only()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        using var sales = AuthorizedClient((await SignInAsync("sales@onyx.test", StrongPassword)).AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/v1/admin/notifications", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await sales.GetAsync("/api/v1/admin/notifications", Token)).StatusCode);
    }

    [Fact]
    public async Task Permission_changes_apply_without_signing_in_again()
    {
        Fixture.RequireDatabase();
        var (_, admin) = await SignInSuperAdminAsync();
        var salesId = await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        using var sales = AuthorizedClient((await SignInAsync("sales@onyx.test", StrongPassword)).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await sales.GetAsync("/api/v1/admin/audit-logs", Token)).StatusCode);

        using var role = await admin.PostAsJsonAsync("/api/v1/admin/roles", new SaveRoleRequest("Auditors", [Permissions.AuditLogs.View]), Token);
        Assert.Equal(HttpStatusCode.OK, role.StatusCode);
        using var assign = await admin.PutAsJsonAsync($"/api/v1/admin/users/{salesId}/roles", new SetUserRolesRequest([SystemRoles.Sales, "Auditors"]), Token);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await sales.GetAsync("/api/v1/admin/audit-logs", Token)).StatusCode);
        admin.Dispose();
    }

    [Fact]
    public void Every_endpoint_declares_its_authorization_explicitly()
    {
        Fixture.RequireDatabase();
        var endpoints = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        var undeclared = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null && endpoint.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.True(undeclared.Count == 0, $"Endpoints without explicit [AllowAnonymous]/[Authorize]/[HasPermission]: {string.Join(", ", undeclared)}");
    }

    [Fact]
    public void Every_permission_used_by_the_api_exists_in_the_catalogue()
    {
        var used = typeof(HasPermissionAttribute).Assembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributes<HasPermissionAttribute>()
                .Concat(type.GetMethods().SelectMany(method => method.GetCustomAttributes<HasPermissionAttribute>())))
            .Select(attribute => attribute.Permission)
            .Distinct()
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, permission => Assert.Contains(permission, Permissions.All));
    }
}
