using Onyx.Contracts.Authorization;

namespace Onyx.Application.Identity;

/// <summary>
/// Who must use an authenticator (CLAUDE.md §12: mandatory for privileged roles). Privileged = SuperAdmin, or anyone who can
/// administer accounts, roles, provider credentials or personal-data operations. Configuration can require it for everyone.
/// </summary>
public static class TwoFactorPolicy
{
    public static IReadOnlySet<string> PrivilegedPermissions { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Permissions.Users.Manage,
        Permissions.Roles.Manage,
        Permissions.ProviderConnections.Manage,
        Permissions.Privacy.Manage,
        Permissions.Leads.Export,
    };

    public static bool IsRequired(bool isSuperAdmin, IReadOnlyCollection<string> permissions, bool requireForAllUsers)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return requireForAllUsers || isSuperAdmin || permissions.Any(PrivilegedPermissions.Contains);
    }
}
