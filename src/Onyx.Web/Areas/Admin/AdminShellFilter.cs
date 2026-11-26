using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Navigation;
using Onyx.Web.Authentication;

namespace Onyx.Web.Areas.Admin;

/// <summary>
/// Loads the admin shell (name, permission-filtered menu, unread count) before the action runs, so a revoked session is
/// handled by <see cref="AdminSessionEndedFilter"/> instead of failing during view rendering.
/// </summary>
public sealed class AdminShellFilter(CurrentAdminContext currentAdmin, IAdminApiClient adminApi) : IAsyncActionFilter
{
    public const string ViewDataKey = "Onyx.AdminShell";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var cancellation = context.HttpContext.RequestAborted;
        var me = await currentAdmin.GetAsync(cancellation).ConfigureAwait(false);
        var unread = await adminApi.GetUnreadNotificationCountAsync(cancellation).ConfigureAwait(false);

        if (context.Controller is Microsoft.AspNetCore.Mvc.Controller controller)
        {
            var permissions = me.Permissions.ToHashSet(StringComparer.Ordinal);
            controller.ViewData[ViewDataKey] = new AdminShellModel(
                me.DisplayName,
                AdminNavigation.VisibleTo(permissions),
                unread.IsSuccess ? unread.Value : 0,
                permissions);
        }

        await next().ConfigureAwait(false);
    }
}
