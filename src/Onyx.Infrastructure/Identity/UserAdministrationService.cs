using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Onyx.Application.Abstractions;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Email;
using Onyx.Application.Identity;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Common;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Infrastructure.Email.Templates;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

public sealed class UserAdministrationService(
    UserManager<ApplicationUser> users,
    ApplicationDbContext dbContext,
    RoleCatalog roles,
    IPermissionService permissions,
    ICurrentAdmin currentAdmin,
    ICurrentUser currentUser,
    SessionManager sessions,
    IAuditTrail auditTrail,
    IEmailOutbox emailOutbox,
    AdminLinkBuilder links,
    IOptions<IdentitySecurityOptions> securityOptions,
    TimeProvider timeProvider,
    IValidator<InviteUserRequest> inviteValidator,
    IValidator<UpdateUserRequest> updateValidator,
    IValidator<SetUserRolesRequest> rolesValidator) : IUserAdministrationService
{
    public const int MaxPageSize = 100;

    private static readonly Error NotFound = Error.NotFound("User.NotFound", "User not found.");
    private static readonly Error InsufficientPrivileges = Error.Forbidden("User.InsufficientPrivileges", "You cannot manage users with permissions you do not hold.");
    private static readonly Error LastSuperAdmin = Error.Conflict("User.LastSuperAdmin", "The last active SuperAdmin cannot be disabled or demoted.");

    public async Task<PagedResult<UserSummaryResponse>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var query = dbContext.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var normalized = term.ToUpperInvariant();
            query = query.Where(user => user.NormalizedEmail!.Contains(normalized) || EF.Functions.ILike(user.DisplayName, "%" + term + "%"));
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var pageUsers = await query.OrderByDescending(user => user.CreatedAtUtc).ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);

        var roleNames = await RoleNamesAsync(pageUsers.Select(user => user.Id).ToList(), cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var items = pageUsers.Select(user => ToSummary(user, roleNames.GetValueOrDefault(user.Id, []), now)).ToList();
        return new PagedResult<UserSummaryResponse>(items, page, pageSize, total);
    }

    public async Task<Result<UserSummaryResponse>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        return user is null ? NotFound : await SummaryAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<UserSummaryResponse>> InviteAsync(InviteUserRequest request, CancellationToken cancellationToken)
    {
        if (await inviteValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var requestedRoles = await ResolveRolesAsync(request.Roles, cancellationToken).ConfigureAwait(false);
        if (requestedRoles.IsFailure)
        {
            return requestedRoles.Error!;
        }

        var (rolePermissions, includesSuperAdmin) = await roles.PermissionsOfAsync(requestedRoles.Value, cancellationToken).ConfigureAwait(false);
        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        if (!PermissionGrantPolicy.CanActOn(actor, rolePermissions, includesSuperAdmin))
        {
            return InsufficientPrivileges;
        }

        if (await users.FindByEmailAsync(request.Email.Trim()).ConfigureAwait(false) is not null)
        {
            return Error.Conflict("User.EmailTaken", "An account with this email already exists.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var user = new ApplicationUser
        {
            Email = request.Email.Trim(),
            UserName = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            PreferredLanguage = request.PreferredLanguage,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            EmailConfirmed = false,
        };

        var created = await users.CreateAsync(user).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return created.ToValidationError("email");
        }

        if (requestedRoles.Value.Count > 0)
        {
            await users.AddToRolesAsync(user, requestedRoles.Value.Select(role => role.Name!)).ConfigureAwait(false);
        }

        var queued = await QueueInvitationAsync(user, cancellationToken).ConfigureAwait(false);
        if (queued.IsFailure)
        {
            return queued.Error!;
        }

        auditTrail.Record(AuditAction.UserInvited, AuditCategory.Security, "User", user.Id.ToString(), new { roles = requestedRoles.Value.Select(role => role.Name) });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return await SummaryAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> ResendInvitationAsync(Guid userId, CancellationToken cancellationToken)
    {
        var (user, guard) = await LoadForManagementAsync(userId, cancellationToken).ConfigureAwait(false);
        if (guard is not null)
        {
            return Result.Failure(guard);
        }

        if (user!.EmailConfirmed || !user.IsActive)
        {
            return Result.Failure(Error.Conflict("User.AlreadyActivated", "This invitation was already accepted or the account is disabled."));
        }

        var queued = await QueueInvitationAsync(user, cancellationToken).ConfigureAwait(false);
        if (queued.IsFailure)
        {
            return queued;
        }

        auditTrail.Record(AuditAction.UserInvited, AuditCategory.Security, "User", user.Id.ToString(), new { resent = true });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<UserSummaryResponse>> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (await updateValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var (user, guard) = await LoadForManagementAsync(userId, cancellationToken, allowSelf: true).ConfigureAwait(false);
        if (guard is not null)
        {
            return guard;
        }

        user!.DisplayName = request.DisplayName.Trim();
        user.PreferredLanguage = request.PreferredLanguage;
        var updated = await users.UpdateAsync(user).ConfigureAwait(false);
        if (!updated.Succeeded)
        {
            return updated.ToValidationError("user");
        }

        await RecordAndSaveAsync(AuditAction.UserUpdated, user.Id, new { fields = new[] { "displayName", "preferredLanguage" } }, cancellationToken).ConfigureAwait(false);
        return await SummaryAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<UserSummaryResponse>> SetRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken)
    {
        if (await rolesValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return NotFound;
        }

        var requestedRoles = await ResolveRolesAsync(request.Roles, cancellationToken).ConfigureAwait(false);
        if (requestedRoles.IsFailure)
        {
            return requestedRoles.Error!;
        }

        var currentRoleNames = await users.GetRolesAsync(user).ConfigureAwait(false);
        var currentRoles = await roles.FindByNamesAsync(currentRoleNames.ToList(), cancellationToken).ConfigureAwait(false);

        // The actor must hold everything the user has now and everything they would get (no escalation, no demoting a superior).
        var (before, hadSuperAdmin) = await roles.PermissionsOfAsync(currentRoles, cancellationToken).ConfigureAwait(false);
        var (after, getsSuperAdmin) = await roles.PermissionsOfAsync(requestedRoles.Value, cancellationToken).ConfigureAwait(false);
        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        if (!PermissionGrantPolicy.CanActOn(actor, before.Union(after).ToList(), hadSuperAdmin || getsSuperAdmin))
        {
            return InsufficientPrivileges;
        }

        if (hadSuperAdmin && !getsSuperAdmin && user.IsActive && await roles.CountOtherActiveSuperAdminsAsync(user.Id, cancellationToken).ConfigureAwait(false) == 0)
        {
            return LastSuperAdmin;
        }

        var requestedNames = requestedRoles.Value.Select(role => role.Name!).ToHashSet(StringComparer.Ordinal);
        var toRemove = currentRoleNames.Where(name => !requestedNames.Contains(name)).ToList();
        var toAdd = requestedNames.Where(name => !currentRoleNames.Contains(name)).ToList();
        if (toRemove.Count > 0)
        {
            await users.RemoveFromRolesAsync(user, toRemove).ConfigureAwait(false);
        }

        if (toAdd.Count > 0)
        {
            await users.AddToRolesAsync(user, toAdd).ConfigureAwait(false);
        }

        permissions.Invalidate(user.Id);
        await EnforceTwoFactorRequirementAsync(user, cancellationToken).ConfigureAwait(false);
        await RecordAndSaveAsync(AuditAction.UserRolesChanged, user.Id, new { from = currentRoleNames, to = requestedNames }, cancellationToken).ConfigureAwait(false);
        return await SummaryAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> DisableAsync(Guid userId, CancellationToken cancellationToken)
    {
        var (user, guard) = await LoadForManagementAsync(userId, cancellationToken).ConfigureAwait(false);
        if (guard is not null)
        {
            return Result.Failure(guard);
        }

        var target = await permissions.GetAsync(user!.Id, cancellationToken).ConfigureAwait(false);
        if (target.IsSuperAdmin && user.IsActive && await roles.CountOtherActiveSuperAdminsAsync(user.Id, cancellationToken).ConfigureAwait(false) == 0)
        {
            return Result.Failure(LastSuperAdmin);
        }

        user.IsActive = false;
        await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        await sessions.RevokeAllAsync(user.Id, SessionManager.ReasonUserDisabled, cancellationToken).ConfigureAwait(false);
        await RecordAndSaveAsync(AuditAction.UserDisabled, user.Id, null, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> EnableAsync(Guid userId, CancellationToken cancellationToken)
    {
        var (user, guard) = await LoadForManagementAsync(userId, cancellationToken).ConfigureAwait(false);
        if (guard is not null)
        {
            return Result.Failure(guard);
        }

        user!.IsActive = true;
        await users.UpdateAsync(user).ConfigureAwait(false);
        await RecordAndSaveAsync(AuditAction.UserEnabled, user.Id, null, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ResetTwoFactorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var (user, guard) = await LoadForManagementAsync(userId, cancellationToken).ConfigureAwait(false);
        if (guard is not null)
        {
            return Result.Failure(guard);
        }

        await users.SetTwoFactorEnabledAsync(user!, false).ConfigureAwait(false);
        await users.ResetAuthenticatorKeyAsync(user!).ConfigureAwait(false);
        await users.RemoveAuthenticationTokenAsync(user!, AuthenticatorEnrollmentBinding.LoginProvider, AuthenticatorEnrollmentBinding.TokenName).ConfigureAwait(false);
        await users.GenerateNewTwoFactorRecoveryCodesAsync(user!, 0).ConfigureAwait(false);
        user!.LastTotpTimeStep = null;
        await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        await sessions.RevokeAllAsync(user.Id, SessionManager.ReasonSecurityChange, cancellationToken).ConfigureAwait(false);
        await RecordAndSaveAsync(AuditAction.TwoFactorReset, user.Id, null, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var (user, guard) = await LoadForManagementAsync(userId, cancellationToken, allowSelf: true).ConfigureAwait(false);
        if (guard is not null)
        {
            return Result.Failure(guard);
        }

        var revoked = await sessions.RevokeAllAsync(user!.Id, SessionManager.ReasonAdministrator, cancellationToken).ConfigureAwait(false);
        await RecordAndSaveAsync(AuditAction.SessionRevoked, user.Id, new { sessions = revoked }, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>Loads a user the current admin may manage: not themselves (unless allowed) and not someone more privileged.</summary>
    private async Task<(ApplicationUser? User, Error? Guard)> LoadForManagementAsync(Guid userId, CancellationToken cancellationToken, bool allowSelf = false)
    {
        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return (null, NotFound);
        }

        if (!allowSelf && currentUser.UserId == user.Id)
        {
            return (null, Error.Conflict("User.CannotManageSelf", "You cannot perform this action on your own account."));
        }

        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        var target = await permissions.GetAsync(user.Id, cancellationToken).ConfigureAwait(false);
        return PermissionGrantPolicy.CanActOn(actor, target.Permissions, target.IsSuperAdmin) ? (user, null) : (null, InsufficientPrivileges);
    }

    private async Task<Result<IReadOnlyList<ApplicationRole>>> ResolveRolesAsync(IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        var found = await roles.FindByNamesAsync(names, cancellationToken).ConfigureAwait(false);
        var missing = names.Where(name => !found.Any(role => string.Equals(role.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
        return missing.Count == 0
            ? Result.Success(found)
            : Result.Failure<IReadOnlyList<ApplicationRole>>(Error.Validation("User.UnknownRole", "Unknown role.", new Dictionary<string, string[]> { ["roles"] = [.. missing] }));
    }

    /// <summary>A user who now needs an authenticator but has none loses their sessions and must enrol at next sign-in.</summary>
    private async Task EnforceTwoFactorRequirementAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var target = await permissions.GetAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (!user.TwoFactorEnabled && TwoFactorPolicy.IsRequired(target.IsSuperAdmin, target.Permissions, securityOptions.Value.RequireTwoFactorForAllUsers))
        {
            await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);
            await sessions.RevokeAllAsync(user.Id, SessionManager.ReasonSecurityChange, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Result> QueueInvitationAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = await users.GenerateUserTokenAsync(user, InvitationTokenProviderOptions.ProviderName, InvitationTokenProviderOptions.Purpose).ConfigureAwait(false);
        var queued = await emailOutbox.QueueAsync(
            new EmailRequest(
                InvitationEmailTemplate.TemplateKey,
                user.PreferredLanguage,
                user.Email!,
                user.DisplayName,
                new Dictionary<string, string>
                {
                    ["name"] = user.DisplayName,
                    ["link"] = links.AcceptInvitation(user.Id, token).AbsoluteUri,
                    ["hours"] = securityOptions.Value.InvitationLifetimeHours.ToString(CultureInfo.InvariantCulture),
                }),
            cancellationToken).ConfigureAwait(false);
        return queued.IsSuccess ? Result.Success() : Result.Failure(queued.Error!);
    }

    private async Task RecordAndSaveAsync(AuditAction action, Guid userId, object? details, CancellationToken cancellationToken)
    {
        auditTrail.Record(action, AuditCategory.Security, "User", userId.ToString(), details);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<UserSummaryResponse> SummaryAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var names = await RoleNamesAsync([user.Id], cancellationToken).ConfigureAwait(false);
        return ToSummary(user, names.GetValueOrDefault(user.Id, []), timeProvider.GetUtcNow());
    }

    private async Task<Dictionary<Guid, List<string>>> RoleNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var rows = await dbContext.UserRoles.AsNoTracking()
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(dbContext.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.GroupBy(row => row.UserId).ToDictionary(group => group.Key, group => group.Select(row => row.Name!).Order(StringComparer.Ordinal).ToList());
    }

    private static UserSummaryResponse ToSummary(ApplicationUser user, List<string> roleNames, DateTimeOffset now) =>
        new(user.Id, user.Email!, user.DisplayName, user.PreferredLanguage, user.IsActive, user.EmailConfirmed, user.TwoFactorEnabled,
            user.LockoutEnd is { } lockoutEnd && lockoutEnd > now, roleNames, user.CreatedAtUtc, user.LastLoginAtUtc);
}
