using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

/// <summary>Effective permissions = union of the user's roles; SuperAdmin implicitly holds the whole catalogue.</summary>
public sealed class PermissionService(ApplicationDbContext dbContext, IMemoryCache cache, IOptions<SessionOptions> options) : IPermissionService
{
    public async Task<ActorPermissions> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var key = CacheKey(userId);
        if (cache.TryGetValue(key, out ActorPermissions? cached) && cached is not null)
        {
            return cached;
        }

        var roles = await dbContext.UserRoles.AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .Join(dbContext.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => new { role.Id, role.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var isSuperAdmin = roles.Any(role => role.Name == SystemRoles.SuperAdmin);
        IReadOnlySet<string> permissions;
        if (isSuperAdmin)
        {
            permissions = Permissions.All.ToHashSet(StringComparer.Ordinal);
        }
        else
        {
            var roleIds = roles.Select(role => role.Id).ToList();
            var granted = await dbContext.RolePermissions.AsNoTracking()
                .Where(permission => roleIds.Contains(permission.RoleId))
                .Select(permission => permission.Permission)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // Ignore stale values no longer in the catalogue.
            permissions = granted.Where(Permissions.All.Contains).ToHashSet(StringComparer.Ordinal);
        }

        var result = new ActorPermissions(userId, isSuperAdmin, permissions);
        if (options.Value.PermissionCacheSeconds > 0)
        {
            cache.Set(key, result, TimeSpan.FromSeconds(options.Value.PermissionCacheSeconds));
        }

        return result;
    }

    public void Invalidate(Guid userId) => cache.Remove(CacheKey(userId));

    public async Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var userIds = await dbContext.UserRoles.AsNoTracking()
            .Where(userRole => userRole.RoleId == roleId)
            .Select(userRole => userRole.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var userId in userIds)
        {
            Invalidate(userId);
        }
    }

    private static string CacheKey(Guid userId) => $"onyx:permissions:{userId:N}";
}
