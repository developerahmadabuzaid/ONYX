using Microsoft.EntityFrameworkCore;
using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

/// <summary>Role → permission lookups shared by the administration services.</summary>
public sealed class RoleCatalog(ApplicationDbContext dbContext)
{
    public async Task<IReadOnlyList<ApplicationRole>> FindByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        var normalized = names.Select(name => name.Trim().ToUpperInvariant()).Distinct().ToList();
        return await dbContext.Roles.Where(role => normalized.Contains(role.NormalizedName!)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Union of the permissions of the given roles; SuperAdmin contributes the whole catalogue.</summary>
    public async Task<(IReadOnlySet<string> Permissions, bool IncludesSuperAdmin)> PermissionsOfAsync(IReadOnlyCollection<ApplicationRole> roles, CancellationToken cancellationToken)
    {
        if (roles.Any(role => role.Name == SystemRoles.SuperAdmin))
        {
            return (Permissions.All.ToHashSet(StringComparer.Ordinal), true);
        }

        var roleIds = roles.Select(role => role.Id).ToList();
        var permissions = await dbContext.RolePermissions.AsNoTracking()
            .Where(permission => roleIds.Contains(permission.RoleId))
            .Select(permission => permission.Permission)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (permissions.ToHashSet(StringComparer.Ordinal), false);
    }

    public async Task<IReadOnlyList<string>> PermissionsOfRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
        await dbContext.RolePermissions.AsNoTracking()
            .Where(permission => permission.RoleId == roleId)
            .Select(permission => permission.Permission)
            .OrderBy(permission => permission)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Active SuperAdmins other than <paramref name="excludingUserId"/>.</summary>
    public Task<int> CountOtherActiveSuperAdminsAsync(Guid excludingUserId, CancellationToken cancellationToken) =>
        dbContext.UserRoles
            .Join(dbContext.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => new { userRole.UserId, role.Name })
            .Join(dbContext.Users, pair => pair.UserId, user => user.Id, (pair, user) => new { pair.Name, user.Id, user.IsActive })
            .CountAsync(row => row.Name == SystemRoles.SuperAdmin && row.IsActive && row.Id != excludingUserId, cancellationToken);
}
