using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Common;

namespace Onyx.API.IntegrationTests.Identity;

public sealed class BootstrapTests(PostgresFixture fixture) : IdentityApiTestBase(fixture)
{
    [Fact]
    public async Task Seeding_is_idempotent_and_creates_the_default_bundles()
    {
        Fixture.RequireDatabase();

        var secondRun = await InScopeAsync(services => services.GetRequiredService<IIdentityBootstrapService>().SeedRolesAsync(Token));
        var roles = await QueryAsync(db => db.Roles);
        var grants = await QueryAsync(db => db.RolePermissions);

        Assert.Equal(0, secondRun);
        Assert.Equal(4, roles.Count);
        Assert.True(roles.Single(role => role.Name == SystemRoles.SuperAdmin).IsSystem);
        var superAdminId = roles.Single(role => role.Name == SystemRoles.SuperAdmin).Id;
        Assert.DoesNotContain(grants, grant => grant.RoleId == superAdminId);
        var salesId = roles.Single(role => role.Name == SystemRoles.Sales).Id;
        Assert.Contains(grants, grant => grant.RoleId == salesId && grant.Permission == Permissions.Leads.View);
    }

    [Fact]
    public async Task The_initial_superadmin_can_be_created_only_once_and_only_with_a_policy_compliant_password()
    {
        Fixture.RequireDatabase();

        var weak = await Bootstrap("owner@onyx.test", "short");
        var created = await Bootstrap("owner@onyx.test", StrongPassword);
        var again = await Bootstrap("second@onyx.test", StrongPassword);

        Assert.Equal(ErrorType.Validation, weak.Error!.Type);
        Assert.True(created.IsSuccess);
        Assert.Equal("Bootstrap.AlreadyInitialized", again.Error!.Code);

        // The bootstrapped SuperAdmin must still enrol an authenticator at first sign-in.
        Assert.Equal("TwoFactorEnrollmentRequired", (await LoginAsync("owner@onyx.test", StrongPassword)).Body!.Status);
    }

    private Task<Result<Guid>> Bootstrap(string email, string password) =>
        InScopeAsync(services => services.GetRequiredService<IIdentityBootstrapService>()
            .CreateInitialSuperAdminAsync(email, "Owner", password, "ar", Token));
}
