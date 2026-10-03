namespace Onyx.Web.Areas.Admin.Navigation;

/// <param name="RequiredPermission">Null means any signed-in admin.</param>
public sealed record AdminNavigationItem(string ResourceKey, string Controller, string Action, string? RequiredPermission);
