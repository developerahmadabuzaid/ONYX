using Onyx.Contracts.Leads;

namespace Onyx.Web.Areas.Admin.Models;

/// <summary>Lead detail with the origin panel. The flags only hide controls; every action is authorized by the API.</summary>
public sealed record LeadDetailViewModel(LeadDetailResponse Lead, IReadOnlyList<LeadAssigneeResponse> Assignees, bool CanManage, bool CanAssign);
