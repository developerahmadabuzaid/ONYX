namespace Onyx.Web.Areas.Admin.Navigation;

/// <param name="RequiredPermission">Null means any signed-in admin.</param>
/// <param name="Group">Resource key of the menu group the item is listed under.</param>
public sealed record AdminNavigationItem(string ResourceKey, string Controller, string Action, string? RequiredPermission, string Group = AdminNavigation.Overview);
