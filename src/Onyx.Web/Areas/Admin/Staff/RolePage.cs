using Onyx.Contracts.Administration;

namespace Onyx.Web.Areas.Admin.Staff;

/// <param name="Role">Null when creating a new role.</param>
public sealed record RolePage(RoleResponse? Role, IReadOnlyList<PermissionResponse> Permissions, IReadOnlySet<string> Selected, string? Name);
