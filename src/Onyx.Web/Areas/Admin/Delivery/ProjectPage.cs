using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Project settings: details, members and work-in-progress limits.</summary>
public sealed record ProjectPage(DeliveryProjectResponse Project, IReadOnlyList<DeliveryUserResponse> EligibleUsers, DeliveryAnalyticsResponse? Analytics = null);
