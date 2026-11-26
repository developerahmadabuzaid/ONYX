using Onyx.Contracts.Leads;

namespace Onyx.Web.Areas.Admin.Models;

/// <summary>The admin overview: live counts read from the API (never estimated) and the latest inquiries.</summary>
public sealed record DashboardModel(
    string DisplayName,
    IReadOnlyList<DashboardCard> Cards,
    IReadOnlyList<InquirySummaryResponse>? RecentInquiries,
    bool SomeUnavailable,
    IReadOnlySet<string> Permissions);

/// <param name="Group">Resource key of the card's area heading.</param>
/// <param name="Attention">True when the count is work waiting for someone (drafts to publish, items to review, new leads).</param>
public sealed record DashboardCard(string Group, string TitleKey, long Value, string Controller, string Action, IReadOnlyDictionary<string, string>? Route = null, bool Attention = false);
