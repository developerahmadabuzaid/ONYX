using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;

namespace Onyx.Web.Areas.Admin.Models;

/// <summary>The lead pipeline list with its filters. <see cref="CanExport"/> only hides the button; the API enforces the permission.</summary>
public sealed record LeadListViewModel(PagedResult<LeadSummaryResponse> Leads, LeadListQuery Query, bool CanExport)
{
    public static IReadOnlyList<string> Stages { get; } = ["New", "Contacted", "Qualified", "Proposal", "Won", "Lost"];

    public static IReadOnlyList<string> Priorities { get; } = ["Low", "Normal", "High"];
}
