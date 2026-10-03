namespace Onyx.Web.Areas.Admin.Navigation;

/// <summary>Admin menu. Items appear only when built; each may require a permission (hidden otherwise).</summary>
public static class AdminNavigation
{
    public static IReadOnlyList<AdminNavigationItem> Items { get; } =
    [
        new("Nav.Dashboard", "Dashboard", "Index", null),
        new("Nav.Notifications", "Notifications", "Index", null),
    ];

    public static IReadOnlyList<AdminNavigationItem> VisibleTo(IReadOnlyCollection<string> permissions, IReadOnlyList<AdminNavigationItem>? items = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return (items ?? Items).Where(item => item.RequiredPermission is null || permissions.Contains(item.RequiredPermission)).ToList();
    }
}
