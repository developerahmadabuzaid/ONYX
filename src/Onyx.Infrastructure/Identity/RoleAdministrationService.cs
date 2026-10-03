using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Identity;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

public sealed class RoleAdministrationService(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> users,
    ApplicationDbContext dbContext,
    RoleCatalog roles,
    IPermissionService permissions,
    ICurrentAdmin currentAdmin,
    SessionManager sessions,
    IAuditTrail auditTrail,
    IOptions<IdentitySecurityOptions> securityOptions,
    IValidator<SaveRoleRequest> validator) : IRoleAdministrationService
{
    private static readonly Error NotFound = Error.NotFound("Role.NotFound", "Role not found.");
    private static readonly Error SystemRoleReadOnly = Error.Forbidden("Role.SystemRoleReadOnly", "The SuperAdmin role cannot be changed.");
    private static readonly Error InsufficientPrivileges = Error.Forbidden("Role.InsufficientPrivileges", "You cannot grant or remove permissions you do not hold.");

    public async Task<IReadOnlyList<RoleResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var allRoles = await dbContext.Roles.AsNoTracking().OrderBy(role => role.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
        var grants = await dbContext.RolePermissions.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var userCounts = await dbContext.UserRoles.AsNoTracking().GroupBy(userRole => userRole.RoleId)
            .Select(group => new { RoleId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.RoleId, row => row.Count, cancellationToken).ConfigureAwait(false);

        return allRoles.Select(role => new RoleResponse(
            role.Id,
            role.Name!,
            role.IsSystem,
            role.IsSystem
                ? Permissions.All.Order(StringComparer.Ordinal).ToList()
                : grants.Where(grant => grant.RoleId == role.Id).Select(grant => grant.Permission).Order(StringComparer.Ordinal).ToList(),
            userCounts.GetValueOrDefault(role.Id))).ToList();
    }

    public async Task<Result<RoleResponse>> CreateAsync(SaveRoleRequest request, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        if (await NameIsUnavailableAsync(request.Name, null).ConfigureAwait(false) is { } nameError)
        {
            return nameError;
        }

        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        if (!PermissionGrantPolicy.CanActOn(actor, request.Permissions, targetInvolvesSuperAdmin: false))
        {
            return InsufficientPrivileges;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var role = new ApplicationRole(request.Name.Trim());
        var created = await roleManager.CreateAsync(role).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return created.ToValidationError("name");
        }

        dbContext.RolePermissions.AddRange(request.Permissions.Select(permission => new RolePermission { RoleId = role.Id, Permission = permission }));
        auditTrail.Record(AuditAction.RoleCreated, AuditCategory.Security, "Role", role.Id.ToString(), new { name = role.Name, permissions = request.Permissions });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(role, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RoleResponse>> UpdateAsync(Guid roleId, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var role = await roleManager.FindByIdAsync(roleId.ToString()).ConfigureAwait(false);
        if (role is null)
        {
            return NotFound;
        }

        if (role.IsSystem)
        {
            return SystemRoleReadOnly;
        }

        if (await NameIsUnavailableAsync(request.Name, role.Id).ConfigureAwait(false) is { } nameError)
        {
            return nameError;
        }

        var existing = await roles.PermissionsOfRoleAsync(role.Id, cancellationToken).ConfigureAwait(false);
        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        if (!PermissionGrantPolicy.CanActOn(actor, existing.Union(request.Permissions).ToList(), targetInvolvesSuperAdmin: false))
        {
            return InsufficientPrivileges;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(role.Name, request.Name.Trim(), StringComparison.Ordinal))
        {
            await roleManager.SetRoleNameAsync(role, request.Name.Trim()).ConfigureAwait(false);
            await roleManager.UpdateAsync(role).ConfigureAwait(false);
        }

        await dbContext.RolePermissions.Where(grant => grant.RoleId == role.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        dbContext.RolePermissions.AddRange(request.Permissions.Select(permission => new RolePermission { RoleId = role.Id, Permission = permission }));
        auditTrail.Record(AuditAction.RoleUpdated, AuditCategory.Security, "Role", role.Id.ToString(), new
        {
            name = request.Name.Trim(),
            added = request.Permissions.Except(existing).ToList(),
            removed = existing.Except(request.Permissions).ToList(),
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        await permissions.InvalidateRoleAsync(role.Id, cancellationToken).ConfigureAwait(false);
        await EnforceTwoFactorRequirementForMembersAsync(role.Id, cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(role, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> DeleteAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString()).ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure(NotFound);
        }

        if (role.IsSystem)
        {
            return Result.Failure(SystemRoleReadOnly);
        }

        var existing = await roles.PermissionsOfRoleAsync(role.Id, cancellationToken).ConfigureAwait(false);
        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        if (!PermissionGrantPolicy.CanActOn(actor, existing, targetInvolvesSuperAdmin: false))
        {
            return Result.Failure(InsufficientPrivileges);
        }

        if (await dbContext.UserRoles.AnyAsync(userRole => userRole.RoleId == role.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.Conflict("Role.InUse", "Remove this role from all users before deleting it."));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.RolePermissions.Where(grant => grant.RoleId == role.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await roleManager.DeleteAsync(role).ConfigureAwait(false);
        auditTrail.Record(AuditAction.RoleDeleted, AuditCategory.Security, "Role", role.Id.ToString(), new { name = role.Name });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Error?> NameIsUnavailableAsync(string name, Guid? excludingRoleId)
    {
        var trimmed = name.Trim();
        if (string.Equals(trimmed, SystemRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return Error.Conflict("Role.NameReserved", "This role name is reserved.");
        }

        var existing = await roleManager.FindByNameAsync(trimmed).ConfigureAwait(false);
        return existing is not null && existing.Id != excludingRoleId
            ? Error.Conflict("Role.NameTaken", "A role with this name already exists.")
            : null;
    }

    private async Task EnforceTwoFactorRequirementForMembersAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var memberIds = await dbContext.UserRoles.AsNoTracking().Where(userRole => userRole.RoleId == roleId)
            .Select(userRole => userRole.UserId).ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var memberId in memberIds)
        {
            var member = await users.FindByIdAsync(memberId.ToString()).ConfigureAwait(false);
            if (member is null || member.TwoFactorEnabled)
            {
                continue;
            }

            var effective = await permissions.GetAsync(member.Id, cancellationToken).ConfigureAwait(false);
            if (TwoFactorPolicy.IsRequired(effective.IsSuperAdmin, effective.Permissions, securityOptions.Value.RequireTwoFactorForAllUsers))
            {
                await users.UpdateSecurityStampAsync(member).ConfigureAwait(false);
                await sessions.RevokeAllAsync(member.Id, SessionManager.ReasonSecurityChange, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<RoleResponse> ToResponseAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        var granted = await roles.PermissionsOfRoleAsync(role.Id, cancellationToken).ConfigureAwait(false);
        var count = await dbContext.UserRoles.CountAsync(userRole => userRole.RoleId == role.Id, cancellationToken).ConfigureAwait(false);
        return new RoleResponse(role.Id, role.Name!, role.IsSystem, granted, count);
    }
}
