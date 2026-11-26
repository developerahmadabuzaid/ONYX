using System.Net;
using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Authorization;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Staff;
using Onyx.Web.Authentication;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Staff accounts and roles. Staff join only by invitation (no public staff sign-up); roles are permission bundles. The API
/// enforces Users.Manage / Roles.Manage and never lets anyone grant a permission they do not hold themselves.
/// </summary>
[Route("admin/staff")]
public sealed class StaffController(IAdminStaffApiClient api, CurrentAdminContext currentAdmin) : CmsControllerBase
{
    /// <summary>Built-in roles, offered when the caller may assign roles but not list them (no Roles.Manage).</summary>
    private static readonly string[] SystemRoleNames = ["SuperAdmin", "ContentManager", "Marketing", "Sales"];

    private static readonly Dictionary<string, string> Commands = new(StringComparer.Ordinal)
    {
        ["resend-invitation"] = "invitation",
        ["disable"] = "disable",
        ["enable"] = "enable",
        ["reset-two-factor"] = "two-factor/reset",
        ["end-sessions"] = "sessions/revoke",
    };

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? search, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        var users = await api.UsersAsync(Blank(search), Math.Max(1, page), cancellationToken);
        if (Failed(users) is { } failed)
        {
            return failed;
        }

        var canManageRoles = await currentAdmin.HasPermissionAsync(Permissions.Roles.Manage, cancellationToken);
        return View(new UsersPage(users.Value!, Blank(search), await RolesForAssignmentAsync(canManageRoles, cancellationToken), canManageRoles));
    }

    [HttpPost("users")]
    public async Task<IActionResult> Invite(InviteForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var invited = await api.InviteAsync(
            new InviteUserRequest((form.Email ?? string.Empty).Trim(), (form.DisplayName ?? string.Empty).Trim(), form.Language == "en" ? "en" : "ar", form.Roles), cancellationToken);
        return invited.IsSuccess
            ? After(invited, RedirectToAction(nameof(UserDetail), new { id = invited.Value!.Id }), "Staff.Invited")
            : After(invited, RedirectToAction(nameof(Users)));
    }

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> UserDetail(Guid id, CancellationToken cancellationToken)
    {
        var user = await api.UserAsync(id, cancellationToken);
        if (Failed(user) is { } failed)
        {
            return failed;
        }

        var me = await currentAdmin.GetAsync(cancellationToken);
        var roles = await RolesForAssignmentAsync(me.Permissions.Contains(Permissions.Roles.Manage, StringComparer.Ordinal), cancellationToken);
        return View(new UserPage(user.Value!, roles, me.Id == id));
    }

    [HttpPost("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromForm] string? displayName, [FromForm] string? language, CancellationToken cancellationToken) =>
        After(await api.UpdateAsync(id, new UpdateUserRequest((displayName ?? string.Empty).Trim(), language == "en" ? "en" : "ar"), cancellationToken),
            RedirectToAction(nameof(UserDetail), new { id }));

    [HttpPost("users/{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, [FromForm] List<string>? roles, CancellationToken cancellationToken) =>
        After(await api.SetRolesAsync(id, new SetUserRolesRequest(roles ?? []), cancellationToken), RedirectToAction(nameof(UserDetail), new { id }));

    [HttpPost("users/{id:guid}/{command:regex(^(resend-invitation|disable|enable|reset-two-factor|end-sessions)$)}")]
    public async Task<IActionResult> UserCommand(Guid id, string command, CancellationToken cancellationToken) =>
        After(await api.UserCommandAsync(id, Commands[command], cancellationToken), RedirectToAction(nameof(UserDetail), new { id }), "Staff.Done." + command);

    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        var roles = await api.RolesAsync(cancellationToken);
        return Failed(roles) ?? View(roles.Value);
    }

    [HttpGet("roles/new")]
    public async Task<IActionResult> NewRole(CancellationToken cancellationToken)
    {
        var permissions = await api.PermissionsAsync(cancellationToken);
        return Failed(permissions) ?? View("Role", new RolePage(null, permissions.Value!, new HashSet<string>(StringComparer.Ordinal), null));
    }

    [HttpPost("roles/new")]
    public async Task<IActionResult> CreateRole([FromForm] string? name, [FromForm] List<string>? permissions, CancellationToken cancellationToken)
    {
        var created = await api.CreateRoleAsync(new SaveRoleRequest((name ?? string.Empty).Trim(), permissions ?? []), cancellationToken);
        if (created.IsSuccess)
        {
            return After(created, RedirectToAction(nameof(Role), new { id = created.Value!.Id }));
        }

        if (created.IsUnavailable || created.StatusCode == HttpStatusCode.Forbidden)
        {
            return After(created, RedirectToAction(nameof(Roles)));
        }

        // Keep what was typed and ticked, and show why it was refused.
        var catalogue = await api.PermissionsAsync(cancellationToken);
        AddProblem(created);
        return Failed(catalogue) ?? View("Role", new RolePage(null, catalogue.Value!, (permissions ?? []).ToHashSet(StringComparer.Ordinal), name));
    }

    [HttpGet("roles/{id:guid}")]
    public async Task<IActionResult> Role(Guid id, CancellationToken cancellationToken)
    {
        var roles = await api.RolesAsync(cancellationToken);
        if (Failed(roles) is { } failed)
        {
            return failed;
        }

        var role = roles.Value!.FirstOrDefault(candidate => candidate.Id == id);
        if (role is null)
        {
            return NotFound();
        }

        var permissions = await api.PermissionsAsync(cancellationToken);
        return Failed(permissions) ?? View(new RolePage(role, permissions.Value!, role.Permissions.ToHashSet(StringComparer.Ordinal), role.Name));
    }

    [HttpPost("roles/{id:guid}")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromForm] string? name, [FromForm] List<string>? permissions, CancellationToken cancellationToken) =>
        After(await api.UpdateRoleAsync(id, new SaveRoleRequest((name ?? string.Empty).Trim(), permissions ?? []), cancellationToken), RedirectToAction(nameof(Role), new { id }));

    [HttpPost("roles/{id:guid}/delete")]
    public async Task<IActionResult> DeleteRole(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await api.DeleteRoleAsync(id, cancellationToken);
        return deleted.IsSuccess ? After(deleted, RedirectToAction(nameof(Roles)), "Staff.RoleDeleted") : After(deleted, RedirectToAction(nameof(Role), new { id }));
    }

    private async Task<IReadOnlyList<RoleResponse>> RolesForAssignmentAsync(bool canListRoles, CancellationToken cancellationToken)
    {
        if (canListRoles)
        {
            var roles = await api.RolesAsync(cancellationToken);
            if (roles.IsSuccess)
            {
                return roles.Value!;
            }
        }

        return SystemRoleNames.Select(name => new RoleResponse(Guid.Empty, name, true, [], 0)).ToList();
    }
}
