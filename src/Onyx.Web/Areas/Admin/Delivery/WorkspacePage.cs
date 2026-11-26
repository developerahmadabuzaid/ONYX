using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>The workspace dashboard: analytics for the chosen period plus the caller's unread notifications count.</summary>
public sealed record WorkspacePage(DeliveryAnalyticsResponse Analytics, int UnreadNotifications, bool CanCreateProjects);
