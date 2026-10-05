using Microsoft.EntityFrameworkCore;
using Onyx.Application.Identity;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

public sealed class UserDirectory(ApplicationDbContext dbContext) : IUserDirectory
{
    public Task<UserContact?> GetContactAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive && user.Email != null)
            .Select(user => new UserContact(user.Id, user.Email!, user.DisplayName, user.PreferredLanguage))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<UserContact>> ListWithPermissionAsync(string permission, CancellationToken cancellationToken)
    {
        var roleIds = dbContext.Roles
            .Where(role => role.Name == SystemRoles.SuperAdmin || dbContext.RolePermissions.Any(granted => granted.RoleId == role.Id && granted.Permission == permission))
            .Select(role => role.Id);

        return await dbContext.Users.AsNoTracking()
            .Where(user => user.IsActive && user.Email != null && dbContext.UserRoles.Any(userRole => userRole.UserId == user.Id && roleIds.Contains(userRole.RoleId)))
            .OrderBy(user => user.DisplayName)
            .Select(user => new UserContact(user.Id, user.Email!, user.DisplayName, user.PreferredLanguage))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken)
            .ConfigureAwait(false);
    }
}
