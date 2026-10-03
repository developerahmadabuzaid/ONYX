namespace Onyx.Application.Identity;

/// <summary>
/// Privilege-escalation guard: an administrator can only grant, remove or act on permissions they hold themselves,
/// and only a SuperAdmin can grant, remove or act on the SuperAdmin role.
/// </summary>
public static class PermissionGrantPolicy
{
    public static bool CanActOn(ActorPermissions actor, IReadOnlyCollection<string> targetPermissions, bool targetInvolvesSuperAdmin)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(targetPermissions);

        if (actor.IsSuperAdmin)
        {
            return true;
        }

        return !targetInvolvesSuperAdmin && targetPermissions.All(actor.Permissions.Contains);
    }
}
