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
}
