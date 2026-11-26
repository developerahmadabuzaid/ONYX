using Onyx.Contracts.Authorization;

namespace Onyx.Web.Areas.Admin.Navigation;

/// <summary>Admin menu. Items appear only when built; each may require a permission (hidden otherwise).</summary>
public static class AdminNavigation
{
    public const string Overview = "Nav.Group.Overview";
    public const string Website = "Nav.Group.Website";
    public const string ContentGroup = "Nav.Group.Content";
    public const string NewsGroup = "Nav.Group.News";
    public const string LeadsGroup = "Nav.Group.Leads";
    public const string AnalyticsGroup = "Nav.Group.Analytics";
    public const string PrivacyGroup = "Nav.Group.Privacy";
    public const string TeamGroup = "Nav.Group.Team";
    public const string DeliveryGroup = "Nav.Group.Delivery";
    public const string CommerceGroup = "Nav.Group.Commerce";

    public static IReadOnlyList<AdminNavigationItem> Items { get; } =
    [
        new("Nav.Dashboard", "Dashboard", "Index", null, Overview),
        new("Nav.Notifications", "Notifications", "Index", null, Overview),

        new("Nav.SitePages", "SiteContent", "Index", Permissions.Pages.View, Website),
        new("Nav.Homepage", "SiteContent", "Home", Permissions.Pages.View, Website),
        new("Nav.SiteNavigation", "SiteContent", "Shared", Permissions.Pages.View, Website),
        new("Nav.SiteSettings", "SiteSettings", "Index", Permissions.Settings.Manage, Website),
        new("Nav.Media", "MediaLibrary", "Index", Permissions.Media.View, Website),
        new("Nav.Ctas", "Ctas", "Index", Permissions.Ctas.View, Website),
        new("Nav.Redirects", "Redirects", "Index", Permissions.Redirects.Manage, Website),

        new("Nav.CommerceProducts", "Commerce", "Products", Permissions.Products.View, CommerceGroup),
        new("Nav.CommerceCustomers", "Commerce", "Customers", Permissions.Customers.View, CommerceGroup),
        new("Nav.CommerceSubscriptions", "Commerce", "Subscriptions", Permissions.Subscriptions.View, CommerceGroup),
        new("Nav.CommerceInvoices", "Commerce", "Invoices", Permissions.Invoices.View, CommerceGroup),

        new("Nav.DeliveryDashboard", "Delivery", "Index", Permissions.Delivery.Access, DeliveryGroup),
        new("Nav.DeliveryProjects", "Delivery", "Projects", Permissions.Delivery.Access, DeliveryGroup),
        new("Nav.DeliveryMyWork", "Delivery", "MyWork", Permissions.Delivery.Access, DeliveryGroup),
        new("Nav.DeliveryIssues", "Delivery", "Issues", Permissions.Delivery.Access, DeliveryGroup),
        new("Nav.DeliverySprints", "DeliveryPlanning", "Sprints", Permissions.Delivery.Access, DeliveryGroup),
        new("Nav.DeliveryRoadmap", "DeliveryPlanning", "Roadmap", Permissions.Delivery.Access, DeliveryGroup),
        new("Nav.DeliveryCalendar", "DeliveryPlanning", "Calendar", Permissions.Delivery.Access, DeliveryGroup),
        new("Nav.DeliveryReports", "DeliveryPlanning", "Reports", Permissions.Delivery.Access, DeliveryGroup),

        new("Nav.Content", "Content", "Index", Permissions.Content.View, ContentGroup),
        new("Nav.Authors", "Authors", "Index", Permissions.Insights.View, ContentGroup),
        new("Nav.Clusters", "Clusters", "Index", Permissions.Content.View, ContentGroup),
        new("Nav.Keywords", "Keywords", "Index", Permissions.Content.View, ContentGroup),
        new("Nav.Recommendations", "Recommendations", "Index", Permissions.Content.View, ContentGroup),
        new("Nav.Intelligence", "Intelligence", "Index", Permissions.Content.View, ContentGroup),
        new("Nav.Calendar", "Calendar", "Index", Permissions.ContentEngine.View, ContentGroup),
        new("Nav.Knowledge", "Knowledge", "Index", Permissions.Knowledge.View, ContentGroup),
        new("Nav.Competitors", "Competitors", "Index", Permissions.Competitors.View, ContentGroup),
        new("Nav.BrandKit", "BrandKit", "Index", Permissions.BrandKit.View, ContentGroup),
        new("Nav.EmailTemplates", "EmailTemplates", "Index", Permissions.EmailTemplates.Manage, ContentGroup),

        new("Nav.News", "NewsItems", "Index", Permissions.News.View, NewsGroup),
        new("Nav.NewsSources", "NewsSources", "Index", Permissions.NewsSources.Manage, NewsGroup),
        new("Nav.NewsCategories", "NewsCategories", "Index", Permissions.News.View, NewsGroup),

        new("Nav.Leads", "Leads", "Index", Permissions.Leads.View, LeadsGroup),
        new("Nav.Inquiries", "Inquiries", "Index", Permissions.Inquiries.View, LeadsGroup),
        new("Nav.Bookings", "Bookings", "Index", Permissions.Bookings.View, LeadsGroup),
        new("Nav.Availability", "Availability", "Index", Permissions.Availability.Manage, LeadsGroup),

        new("Nav.Analytics", "Analytics", "Index", Permissions.Analytics.View, AnalyticsGroup),
        new("Nav.SearchPerformance", "Analytics", "Search", Permissions.SearchPerformance.View, AnalyticsGroup),
        new("Nav.AnalyticsIntegrations", "Analytics", "Integrations", Permissions.ProviderConnections.Manage, AnalyticsGroup),

        new("Nav.PrivacyRequests", "PrivacyRequests", "Index", Permissions.Privacy.Manage, PrivacyGroup),
        new("Nav.Retention", "Retention", "Index", Permissions.Privacy.Manage, PrivacyGroup),

        new("Nav.StaffUsers", "Staff", "Users", Permissions.Users.Manage, TeamGroup),
        new("Nav.StaffRoles", "Staff", "Roles", Permissions.Roles.Manage, TeamGroup),
    ];

    public static IReadOnlyList<AdminNavigationItem> VisibleTo(IReadOnlyCollection<string> permissions, IReadOnlyList<AdminNavigationItem>? items = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return (items ?? Items).Where(item => item.RequiredPermission is null || permissions.Contains(item.RequiredPermission)).ToList();
    }
}
