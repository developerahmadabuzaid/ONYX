using Onyx.Contracts.Administration;
using Onyx.Contracts.Common;

namespace Onyx.Web.ApiClients;

/// <summary>Staff accounts, roles and the permission catalogue (Users.Manage / Roles.Manage; the API enforces both).</summary>
public interface IAdminStaffApiClient
{
    Task<ApiResult<PagedResult<UserSummaryResponse>>> UsersAsync(string? search, int page, CancellationToken cancellationToken);

    Task<ApiResult<UserSummaryResponse>> UserAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<UserSummaryResponse>> InviteAsync(InviteUserRequest request, CancellationToken cancellationToken);

    Task<ApiResult<UserSummaryResponse>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<ApiResult<UserSummaryResponse>> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken cancellationToken);

    /// <summary><paramref name="command"/>: invitation, disable, enable, two-factor/reset or sessions/revoke.</summary>
    Task<ApiResult<NoContent>> UserCommandAsync(Guid id, string command, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<RoleResponse>>> RolesAsync(CancellationToken cancellationToken);

    Task<ApiResult<RoleResponse>> CreateRoleAsync(SaveRoleRequest request, CancellationToken cancellationToken);

    Task<ApiResult<RoleResponse>> UpdateRoleAsync(Guid id, SaveRoleRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteRoleAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<PermissionResponse>>> PermissionsAsync(CancellationToken cancellationToken);
}
