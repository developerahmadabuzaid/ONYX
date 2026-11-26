using System.Globalization;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Common;

namespace Onyx.Web.ApiClients;

public sealed class AdminStaffApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IAdminStaffApiClient
{
    private const string Users = "api/v1/admin/users";
    private const string Roles = "api/v1/admin/roles";

    private static readonly HashSet<string> UserCommands = new(StringComparer.Ordinal) { "invitation", "disable", "enable", "two-factor/reset", "sessions/revoke" };

    public Task<ApiResult<PagedResult<UserSummaryResponse>>> UsersAsync(string? search, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<UserSummaryResponse>>(
            $"{Users}?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page.ToString(CultureInfo.InvariantCulture)}&pageSize=25", cancellationToken);

    public Task<ApiResult<UserSummaryResponse>> UserAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<UserSummaryResponse>($"{Users}/{id:D}", cancellationToken);

    public Task<ApiResult<UserSummaryResponse>> InviteAsync(InviteUserRequest request, CancellationToken cancellationToken) =>
        PostAsync<UserSummaryResponse>(Users, request, cancellationToken);

    public Task<ApiResult<UserSummaryResponse>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        PutAsync<UserSummaryResponse>($"{Users}/{id:D}", request, cancellationToken);

    public Task<ApiResult<UserSummaryResponse>> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken cancellationToken) =>
        PutAsync<UserSummaryResponse>($"{Users}/{id:D}/roles", request, cancellationToken);

    public Task<ApiResult<NoContent>> UserCommandAsync(Guid id, string command, CancellationToken cancellationToken) =>
        UserCommands.Contains(command)
            ? PostAsync<NoContent>($"{Users}/{id:D}/{command}", null, cancellationToken)
            : throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown user command.");

    public Task<ApiResult<IReadOnlyList<RoleResponse>>> RolesAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<RoleResponse>>(Roles, cancellationToken);

    public Task<ApiResult<RoleResponse>> CreateRoleAsync(SaveRoleRequest request, CancellationToken cancellationToken) =>
        PostAsync<RoleResponse>(Roles, request, cancellationToken);

    public Task<ApiResult<RoleResponse>> UpdateRoleAsync(Guid id, SaveRoleRequest request, CancellationToken cancellationToken) =>
        PutAsync<RoleResponse>($"{Roles}/{id:D}", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteRoleAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"{Roles}/{id:D}", cancellationToken);

    public Task<ApiResult<IReadOnlyList<PermissionResponse>>> PermissionsAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<PermissionResponse>>("api/v1/admin/permissions", cancellationToken);
}
