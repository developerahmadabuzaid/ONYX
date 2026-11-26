using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Issue search results (also My Work, where <see cref="Mine"/> is true).</summary>
public sealed record IssuesPage(PagedResult<IssueListItemResponse> Results, IReadOnlyList<DeliveryProjectListItemResponse> Projects, IssueFilter Filter, bool Mine, IReadOnlyList<SavedIssueFilterResponse>? SavedFilters = null, IReadOnlyList<DeliveryUserResponse>? People = null);
