using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onyx.Web.ApiClients;
using Onyx.Web.Authentication;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>Notification Center. The API already filters notifications by the admin's permissions.</summary>
[Area("Admin")]
[Authorize]
[Route("admin/notifications")]
[TypeFilter<AdminShellFilter>]
public sealed class NotificationsController(IAdminApiClient adminApi) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await adminApi.ListNotificationsAsync(Math.Max(page, 1), cancellationToken);
        return result.IsSuccess ? View(result.Value) : throw new ApiUnavailableException();
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await adminApi.MarkNotificationReadAsync(notificationId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
