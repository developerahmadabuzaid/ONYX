using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Active sprints across the visible projects, and the projects to plan in.</summary>
public sealed record SprintsOverviewPage(IReadOnlyList<ActiveSprintSummaryResponse> ActiveSprints, IReadOnlyList<DeliveryProjectListItemResponse> Projects);
