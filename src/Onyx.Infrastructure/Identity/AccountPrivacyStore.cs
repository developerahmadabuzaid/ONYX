using Microsoft.EntityFrameworkCore;
using Onyx.Application.Identity;
using Onyx.Application.Privacy;
using Onyx.Domain.Common;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Data-subject export and erasure of an admin account (identity data, deferred from Stage 3). The account row is kept (other
/// records reference its id: lead owners, note authors, audit entries) but nothing identifying or usable for sign-in remains.
/// </summary>
internal sealed class AccountPrivacyStore(
    ApplicationDbContext dbContext,
    RoleCatalog roles,
    SessionManager sessions,
    IPermissionService permissions) : IAccountPrivacyStore
{
    public const string ErasedDisplayName = "Erased user";
    public const string ErasedAuditUserName = "erased-user";

    private static readonly Error SelfErasure = Error.Conflict("Privacy.SelfErasure", "Operators cannot erase their own account.");
    private static readonly Error LastSuperAdmin = Error.Conflict("Privacy.LastSuperAdmin", "The last active SuperAdmin cannot be erased.");

    public async Task<AccountExport?> ExportAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var user = await FindAsync(normalizedEmail, track: false, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        var roleNames = await dbContext.UserRoles.AsNoTracking().Where(userRole => userRole.UserId == user.Id)
            .Join(dbContext.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var userSessions = await dbContext.UserSessions.AsNoTracking().Where(session => session.UserId == user.Id)
            .OrderBy(session => session.CreatedAtUtc).ThenBy(session => session.Id)
            .Select(session => new AccountSessionExport(session.CreatedAtUtc, session.LastRefreshedAtUtc, session.AbsoluteExpiresAtUtc, session.RevokedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var activity = await dbContext.AuditLogs.AsNoTracking().Where(log => log.UserId == user.Id)
            .OrderBy(log => log.OccurredAtUtc).ThenBy(log => log.Id)
            .Select(log => new { log.OccurredAtUtc, log.Action, log.Category, log.EntityType })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AccountExport(
            user.Id, user.Email!, user.DisplayName, user.PreferredLanguage, user.IsActive, user.EmailConfirmed, user.TwoFactorEnabled, user.CreatedAtUtc,
            user.LastLoginAtUtc, roleNames, userSessions,
            activity.Select(entry => new AccountActivityExport(entry.OccurredAtUtc, entry.Action.ToString(), entry.Category.ToString(), entry.EntityType)).ToList());
    }

    public async Task<Result<Guid?>> EraseAsync(string normalizedEmail, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await FindAsync(normalizedEmail, track: true, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result<Guid?>.Success(null);
        }

        if (user.Id == actorId)
        {
            return SelfErasure;
        }

        var isSuperAdmin = await dbContext.UserRoles.AnyAsync(
            userRole => userRole.UserId == user.Id && dbContext.Roles.Any(role => role.Id == userRole.RoleId && role.Name == SystemRoles.SuperAdmin), cancellationToken).ConfigureAwait(false);
        if (isSuperAdmin && user.IsActive && await roles.CountOtherActiveSuperAdminsAsync(user.Id, cancellationToken).ConfigureAwait(false) == 0)
        {
            return LastSuperAdmin;
        }

        // Revoke first (also clears the session-validation cache), then remove what could still sign in or identify.
        await sessions.RevokeAllAsync(user.Id, SessionManager.ReasonDataSubjectErasure, cancellationToken).ConfigureAwait(false);
        await dbContext.RefreshTokens.Where(token => dbContext.UserSessions.Any(session => session.Id == token.SessionId && session.UserId == user.Id))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.UserSessions.Where(session => session.UserId == user.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.IpAddress, (string?)null), cancellationToken).ConfigureAwait(false);
        await dbContext.LoginChallenges.Where(challenge => challenge.UserId == user.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.UserTokens.Where(token => token.UserId == user.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.UserLogins.Where(login => login.UserId == user.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.UserClaims.Where(claim => claim.UserId == user.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.UserRoles.Where(userRole => userRole.UserId == user.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.AuditLogs.Where(log => log.UserId == user.Id && log.UserName != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(log => log.UserName, ErasedAuditUserName), cancellationToken).ConfigureAwait(false);

        var placeholder = Domain.Privacy.ErasedValues.Email(user.Id);
        user.Email = placeholder;
        user.NormalizedEmail = placeholder.ToUpperInvariant();
        user.UserName = placeholder;
        user.NormalizedUserName = placeholder.ToUpperInvariant();
        user.EmailConfirmed = false;
        user.DisplayName = ErasedDisplayName;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.PasswordHash = null;
        user.TwoFactorEnabled = false;
        user.LastTotpTimeStep = null;
        user.LastLoginAtUtc = null;
        user.IsActive = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        permissions.Invalidate(user.Id);
        return Result<Guid?>.Success(user.Id);
    }

    private Task<ApplicationUser?> FindAsync(string normalizedEmail, bool track, CancellationToken cancellationToken)
    {
        var normalized = normalizedEmail.Trim().ToUpperInvariant();
        var query = track ? dbContext.Users : dbContext.Users.AsNoTracking();
        return query.SingleOrDefaultAsync(user => user.NormalizedEmail == normalized, cancellationToken);
    }
}
