using Onyx.Contracts.Administration;
using Onyx.Contracts.Common;

namespace Onyx.Web.Areas.Admin.Staff;

public sealed record UsersPage(PagedResult<UserSummaryResponse> Users, string? Search, IReadOnlyList<RoleResponse> Roles, bool CanManageRoles);
