using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>
/// Issues with a due date in the visible month or week. <see cref="Truncated"/> is set when more issues are due than one page
/// of the search holds (the page then says so instead of hiding it).
/// </summary>
public sealed record CalendarPage(
    string View,
    DateOnly Anchor,
    DateOnly From,
    DateOnly To,
    DateOnly Today,
    IReadOnlyList<IssueListItemResponse> Issues,
    IReadOnlyList<DeliveryProjectListItemResponse> Projects,
    Guid? ProjectId,
    bool Truncated);
