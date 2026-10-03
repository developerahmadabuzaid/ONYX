using Onyx.Web.Areas.Admin.Navigation;

namespace Onyx.Web.Areas.Admin;

public sealed record AdminShellModel(string DisplayName, IReadOnlyList<AdminNavigationItem> Navigation, int UnreadNotifications);
