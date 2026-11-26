using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Reports for every visible project or one, over the chosen period.</summary>
public sealed record ReportsPage(DeliveryAnalyticsResponse Analytics, IReadOnlyList<DeliveryProjectListItemResponse> Projects, Guid? ProjectId);
