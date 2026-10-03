using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Onyx.Application.Auditing;
using Onyx.Application.Identity;
using Onyx.Application.Localization;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

public sealed class IdentityBootstrapService(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> users,
    ApplicationDbContext dbContext,
    IAuditTrail auditTrail,
    TimeProvider timeProvider) : IIdentityBootstrapService
{
    public async Task<int> SeedRolesAsync(CancellationToken cancellationToken)
    {
        var created = 0;
        if (await roleManager.FindByNameAsync(SystemRoles.SuperAdmin).ConfigureAwait(false) is null)
        {
            await roleManager.CreateAsync(new ApplicationRole(SystemRoles.SuperAdmin) { IsSystem = true }).ConfigureAwait(false);
            created++;
        }

        foreach (var (name, bundle) in SystemRoles.DefaultBundles)
        {
            if (await roleManager.FindByNameAsync(name).ConfigureAwait(false) is not null)
            {
                continue;
            }

            var role = new ApplicationRole(name);
            await roleManager.CreateAsync(role).ConfigureAwait(false);
            dbContext.RolePermissions.AddRange(bundle.Select(permission => new RolePermission { RoleId = role.Id, Permission = permission }));
            created++;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return created;
    }

    public async Task<Result<Guid>> CreateInitialSuperAdminAsync(
        string email, string displayName, string password, string preferredLanguage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal) || string.IsNullOrWhiteSpace(displayName)
            || !SupportedLanguages.All.Contains(preferredLanguage))
        {
            return Error.Validation("Bootstrap.Invalid", "A valid email, display name and supported language are required.");
        }

        await SeedRolesAsync(cancellationToken).ConfigureAwait(false);

        var superAdminRole = await roleManager.FindByNameAsync(SystemRoles.SuperAdmin).ConfigureAwait(false);
        if (await dbContext.UserRoles.AnyAsync(userRole => userRole.RoleId == superAdminRole!.Id, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Bootstrap.AlreadyInitialized", "A SuperAdmin already exists. Use the admin interface to manage users.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var user = new ApplicationUser
        {
            Email = email.Trim(),
            UserName = email.Trim(),
            DisplayName = displayName.Trim(),
            PreferredLanguage = preferredLanguage,
            EmailConfirmed = true,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };

        var created = await users.CreateAsync(user, password).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return created.ToValidationError();
        }

        await users.AddToRoleAsync(user, SystemRoles.SuperAdmin).ConfigureAwait(false);
        auditTrail.Record(AuditAction.InitialAdministratorCreated, AuditCategory.Security, "User", user.Id.ToString(), actorUserId: user.Id);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return user.Id;
    }
}
