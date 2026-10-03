using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;

namespace Onyx.API.IntegrationTests.Identity;

/// <summary>Privilege-escalation and lock-out-of-the-system guards.</summary>
public sealed class AdministrationGuardTests(PostgresFixture fixture) : IdentityApiTestBase(fixture)
{
    [Fact]
    public async Task A_user_manager_cannot_grant_superadmin_or_permissions_they_do_not_hold()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("owner@onyx.test", StrongPassword, SystemRoles.SuperAdmin);
        using var manager = await SignInUserManagerAsync();
        var targetId = await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);

        using var grantSuperAdmin = await manager.PutAsJsonAsync($"/api/v1/admin/users/{targetId}/roles", new SetUserRolesRequest([SystemRoles.SuperAdmin]), Token);
        using var grantContent = await manager.PutAsJsonAsync($"/api/v1/admin/users/{targetId}/roles", new SetUserRolesRequest([SystemRoles.ContentManager]), Token);
        using var inviteAdmin = await manager.PostAsJsonAsync("/api/v1/admin/users", new InviteUserRequest("evil@onyx.test", "Evil", "en", [SystemRoles.SuperAdmin]), Token);
        using var createRole = await manager.PostAsJsonAsync("/api/v1/admin/roles", new SaveRoleRequest("Escalated", [Permissions.Privacy.Manage]), Token);

        Assert.Equal(HttpStatusCode.Forbidden, grantSuperAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, grantContent.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, inviteAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createRole.StatusCode);
    }

    [Fact]
    public async Task A_user_manager_cannot_disable_reset_or_revoke_a_superadmin()
    {
        Fixture.RequireDatabase();
        var ownerId = await CreateUserAsync("owner@onyx.test", StrongPassword, SystemRoles.SuperAdmin);
        using var manager = await SignInUserManagerAsync();

        using var disable = await manager.PostAsync($"/api/v1/admin/users/{ownerId}/disable", null, Token);
        using var reset = await manager.PostAsync($"/api/v1/admin/users/{ownerId}/two-factor/reset", null, Token);
        using var revoke = await manager.PostAsync($"/api/v1/admin/users/{ownerId}/sessions/revoke", null, Token);

        Assert.Equal(HttpStatusCode.Forbidden, disable.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, revoke.StatusCode);
    }

    [Fact]
    public async Task The_last_active_superadmin_cannot_be_demoted_and_nobody_can_disable_themselves()
    {
        Fixture.RequireDatabase();
        var (ownerId, owner) = await SignInSuperAdminAsync();

        using var demote = await owner.PutAsJsonAsync($"/api/v1/admin/users/{ownerId}/roles", new SetUserRolesRequest([SystemRoles.Sales]), Token);
        using var disableSelf = await owner.PostAsync($"/api/v1/admin/users/{ownerId}/disable", null, Token);

        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal("User.LastSuperAdmin", (await demote.Content.ReadFromJsonAsync<ApiProblem>(Token))!.Code);
        Assert.Equal(HttpStatusCode.Conflict, disableSelf.StatusCode);
        owner.Dispose();
    }

    [Fact]
    public async Task The_superadmin_role_is_read_only_and_roles_in_use_cannot_be_deleted()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var roles = await owner.GetFromJsonAsync<List<RoleResponse>>("/api/v1/admin/roles", Token);
        var superAdmin = roles!.Single(role => role.Name == SystemRoles.SuperAdmin);
        var sales = roles!.Single(role => role.Name == SystemRoles.Sales);
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);

        using var edit = await owner.PutAsJsonAsync($"/api/v1/admin/roles/{superAdmin.Id}", new SaveRoleRequest("SuperAdmin", []), Token);
        using var delete = await owner.DeleteAsync($"/api/v1/admin/roles/{superAdmin.Id}", Token);
        using var deleteInUse = await owner.DeleteAsync($"/api/v1/admin/roles/{sales.Id}", Token);
        using var reserved = await owner.PostAsJsonAsync("/api/v1/admin/roles", new SaveRoleRequest("superadmin", []), Token);
        using var unknownPermission = await owner.PostAsJsonAsync("/api/v1/admin/roles", new SaveRoleRequest("Typos", ["Leads.Veiw"]), Token);

        Assert.True(superAdmin.IsSystem);
        Assert.Equal(Permissions.All.Count, superAdmin.Permissions.Count);
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleteInUse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reserved.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownPermission.StatusCode);
        owner.Dispose();
    }

    [Fact]
    public async Task Granting_a_privileged_permission_forces_authenticator_enrolment()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var salesId = await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var salesTokens = await SignInAsync("sales@onyx.test", StrongPassword);
        await owner.PostAsJsonAsync("/api/v1/admin/roles", new SaveRoleRequest("Exporters", [Permissions.Leads.Export]), Token);

        using var assign = await owner.PutAsJsonAsync($"/api/v1/admin/users/{salesId}/roles", new SetUserRolesRequest([SystemRoles.Sales, "Exporters"]), Token);

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        using var sales = AuthorizedClient(salesTokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await sales.GetAsync("/api/v1/auth/me", Token)).StatusCode);
        Assert.Equal("TwoFactorEnrollmentRequired", (await LoginAsync("sales@onyx.test", StrongPassword)).Body!.Status);
        owner.Dispose();
    }

    /// <summary>A non-SuperAdmin holding Users.Manage + Roles.Manage plus a Sales bundle (privileged, so enrolled with 2FA).</summary>
    private async Task<HttpClient> SignInUserManagerAsync()
    {
        await CreateRoleAsync("UserManagers", Permissions.Users.Manage, Permissions.Roles.Manage);
        await CreateUserAsync("manager@onyx.test", StrongPassword, "UserManagers", SystemRoles.Sales);
        var tokens = await SignInAsync("manager@onyx.test", StrongPassword);
        return AuthorizedClient(tokens.AccessToken);
    }
}
