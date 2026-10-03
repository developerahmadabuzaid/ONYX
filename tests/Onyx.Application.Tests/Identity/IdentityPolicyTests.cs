using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;

namespace Onyx.Application.Tests.Identity;

public sealed class IdentityPolicyTests
{
    private static ActorPermissions Actor(bool superAdmin, params string[] permissions) =>
        new(Guid.NewGuid(), superAdmin, permissions.ToHashSet(StringComparer.Ordinal));

    [Fact]
    public void Superadmins_can_act_on_anything()
    {
        Assert.True(PermissionGrantPolicy.CanActOn(Actor(true), Permissions.All, targetInvolvesSuperAdmin: true));
    }

    [Fact]
    public void Others_can_only_act_on_permissions_they_hold_and_never_on_superadmin()
    {
        var manager = Actor(false, Permissions.Users.Manage, Permissions.Leads.View);

        Assert.True(PermissionGrantPolicy.CanActOn(manager, [Permissions.Leads.View], false));
        Assert.False(PermissionGrantPolicy.CanActOn(manager, [Permissions.Leads.View, Permissions.Leads.Export], false));
        Assert.False(PermissionGrantPolicy.CanActOn(manager, [], targetInvolvesSuperAdmin: true));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Two_factor_is_required_for_superadmins_or_when_configured_for_everyone(bool superAdmin, bool requireForAll, bool expected)
    {
        Assert.Equal(expected, TwoFactorPolicy.IsRequired(superAdmin, [Permissions.Leads.View], requireForAll));
    }

    [Theory]
    [InlineData("Users.Manage")]
    [InlineData("Roles.Manage")]
    [InlineData("ProviderConnections.Manage")]
    [InlineData("Privacy.Manage")]
    [InlineData("Leads.Export")]
    public void Two_factor_is_required_for_privileged_permissions(string permission)
    {
        Assert.True(TwoFactorPolicy.IsRequired(false, [permission], false));
    }

    [Fact]
    public void Default_role_bundles_use_only_catalogue_permissions_and_avoid_privileged_ones()
    {
        foreach (var (role, bundle) in SystemRoles.DefaultBundles)
        {
            Assert.All(bundle, permission => Assert.Contains(permission, Permissions.All));
            Assert.False(bundle.Any(TwoFactorPolicy.PrivilegedPermissions.Contains), $"{role} contains a privileged permission.");
            Assert.Equal(bundle.Count, bundle.Distinct().Count());
        }
    }
}
