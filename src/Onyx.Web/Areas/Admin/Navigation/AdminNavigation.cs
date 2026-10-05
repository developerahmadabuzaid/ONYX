using Onyx.Contracts.Authorization;

namespace Onyx.Web.Areas.Admin.Navigation;

/// <summary>Admin menu. Items appear only when built; each may require a permission (hidden otherwise).</summary>
public static class AdminNavigation
{
    public static IReadOnlyList<AdminNavigationItem> Items { get; } =
    [
        new("Nav.Dashboard", "Dashboard", "Index", null),
        new("Nav.Notifications", "Notifications", "Index", null),
        new("Nav.Content", "Content", "Index", Permissions.Content.View),
        new("Nav.Authors", "Authors", "Index", Permissions.Insights.View),
        new("Nav.Media", "MediaLibrary", "Index", Permissions.Media.View),
        new("Nav.Redirects", "Redirects", "Index", Permissions.Redirects.Manage),
        new("Nav.Ctas", "Ctas", "Index", Permissions.Ctas.View),
        new("Nav.Clusters", "Clusters", "Index", Permissions.Content.View),
        new("Nav.Keywords", "Keywords", "Index", Permissions.Content.View),
        new("Nav.Recommendations", "Recommendations", "Index", Permissions.Content.View),
        new("Nav.Intelligence", "Intelligence", "Index", Permissions.Content.View),
        new("Nav.Calendar", "Calendar", "Index", Permissions.ContentEngine.View),
        new("Nav.Competitors", "Competitors", "Index", Permissions.Competitors.View),
        new("Nav.Knowledge", "Knowledge", "Index", Permissions.Knowledge.View),
        new("Nav.BrandKit", "BrandKit", "Index", Permissions.BrandKit.View),
        new("Nav.EmailTemplates", "EmailTemplates", "Index", Permissions.EmailTemplates.Manage),
    ];

    public static IReadOnlyList<AdminNavigationItem> VisibleTo(IReadOnlyCollection<string> permissions, IReadOnlyList<AdminNavigationItem>? items = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return (items ?? Items).Where(item => item.RequiredPermission is null || permissions.Contains(item.RequiredPermission)).ToList();
    }
}
