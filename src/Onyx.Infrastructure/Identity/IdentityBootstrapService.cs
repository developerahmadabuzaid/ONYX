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
    SessionManager sessions,
    IAuditTrail auditTrail,
    TimeProvider timeProvider) : IIdentityBootstrapService
{
    // Identity's own token provider name for the authenticator key and recovery codes.
    private const string IdentityTokenProvider = "[AspNetUserStore]";

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

    public async Task<Result<TwoFactorRecoveryResult>> RecoverSuperAdminTwoFactorAsync(
        string email, string reason, string operatorIdentity, CancellationToken cancellationToken)
    {
        var trimmedReason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(operatorIdentity)
            || trimmedReason.Length is < IdentityRecoveryLimits.ReasonMinLength or > IdentityRecoveryLimits.ReasonMaxLength || trimmedReason.Any(char.IsControl))
        {
            return Error.Validation(
                "Recovery.Invalid",
                $"A target email, the operator identity and a reason of {IdentityRecoveryLimits.ReasonMinLength}-{IdentityRecoveryLimits.ReasonMaxLength} characters are required.");
        }

        var user = await users.FindByEmailAsync(email.Trim()).ConfigureAwait(false);
        if (user is null || !await users.IsInRoleAsync(user, SystemRoles.SuperAdmin).ConfigureAwait(false))
        {
            // Same answer for "no such account" and "not a SuperAdmin": other accounts are reset through the admin API.
            return Error.NotFound("Recovery.NotASuperAdmin", "No SuperAdmin account with that email exists. Other users are reset in the admin portal.");
        }

        if (!user.IsActive)
        {
            return Error.Conflict("Recovery.Inactive", "The account is disabled. Recovery applies only to active SuperAdmin accounts.");
        }

        var superAdminRole = await roleManager.FindByNameAsync(SystemRoles.SuperAdmin).ConfigureAwait(false);
        var anotherEnrolledSuperAdmin = await dbContext.UserRoles
            .Where(userRole => userRole.RoleId == superAdminRole!.Id && userRole.UserId != user.Id)
            .Join(dbContext.Users, userRole => userRole.UserId, other => other.Id, (_, other) => other)
            .AnyAsync(other => other.IsActive && other.TwoFactorEnabled, cancellationToken)
            .ConfigureAwait(false);
        if (anotherEnrolledSuperAdmin)
        {
            return Error.Conflict(
                "Recovery.NotLastSuperAdmin",
                "Another active SuperAdmin with two-factor authentication exists. Ask them to reset this account in the admin portal.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();

        await users.SetTwoFactorEnabledAsync(user, false).ConfigureAwait(false);
        await users.RemoveAuthenticationTokenAsync(user, IdentityTokenProvider, "AuthenticatorKey").ConfigureAwait(false);
        await users.RemoveAuthenticationTokenAsync(user, IdentityTokenProvider, "RecoveryCodes").ConfigureAwait(false);
        await users.RemoveAuthenticationTokenAsync(user, AuthenticatorEnrollmentBinding.LoginProvider, AuthenticatorEnrollmentBinding.TokenName).ConfigureAwait(false);
        user.LastTotpTimeStep = null;

        // A locked-out owner is the usual reason for recovery; the password is still required to sign in.
        await users.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
        await users.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
        await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);

        var challengesClosed = await dbContext.LoginChallenges
            .Where(challenge => challenge.UserId == user.Id && challenge.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.ConsumedAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
        var sessionsRevoked = await sessions.RevokeAllAsync(user.Id, SessionManager.ReasonSecurityChange, cancellationToken).ConfigureAwait(false);

        auditTrail.Record(
            AuditAction.TwoFactorRecoveredByOperator,
            AuditCategory.Security,
            "User",
            user.Id.ToString(),
            new { reason = trimmedReason, @operator = operatorIdentity, sessionsRevoked, challengesClosed, lockoutCleared = true });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new TwoFactorRecoveryResult(user.Id, sessionsRevoked, challengesClosed);
    }
}
