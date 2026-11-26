using Onyx.Web.Areas.Admin.Navigation;

namespace Onyx.Web.Areas.Admin;

/// <summary>What the Admin shell needs on every page. <see cref="Permissions"/> only decides which shortcuts to show; the API enforces.</summary>
public sealed record AdminShellModel(string DisplayName, IReadOnlyList<AdminNavigationItem> Navigation, int UnreadNotifications, IReadOnlySet<string>? Permissions = null)
{
    public bool Can(string permission) => Permissions?.Contains(permission) == true;
}
