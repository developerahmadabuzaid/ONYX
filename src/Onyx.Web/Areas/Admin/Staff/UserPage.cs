using Onyx.Contracts.Administration;

namespace Onyx.Web.Areas.Admin.Staff;

public sealed record UserPage(UserSummaryResponse User, IReadOnlyList<RoleResponse> Roles, bool IsSelf);
