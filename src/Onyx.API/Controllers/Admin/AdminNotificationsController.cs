using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Http;
using Onyx.Application.Identity;
using Onyx.Application.Notifications;
using Onyx.Contracts.Common;
using Onyx.Contracts.Notifications;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// Any authenticated admin may use the Notification Center; each notification is filtered by its own required permission,
/// so users only ever see what their permissions allow.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/notifications")]
[Authorize]
public sealed class AdminNotificationsController(INotificationService notifications, ICurrentAdmin currentAdmin) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<AdminNotificationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminNotificationResponse>>> List(
        [FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var viewer = await currentAdmin.GetNotificationViewerAsync(cancellationToken);
        return Ok(await notifications.ListAsync(viewer, unreadOnly, page, pageSize, cancellationToken));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> UnreadCount(CancellationToken cancellationToken)
    {
        var viewer = await currentAdmin.GetNotificationViewerAsync(cancellationToken);
        return Ok(await notifications.CountUnreadAsync(viewer, cancellationToken));
    }

    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var viewer = await currentAdmin.GetNotificationViewerAsync(cancellationToken);
        return this.NoContentOrProblem(await notifications.MarkReadAsync(viewer, notificationId, cancellationToken));
    }

    [HttpPost("read-all")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> MarkAllRead(CancellationToken cancellationToken)
    {
        var viewer = await currentAdmin.GetNotificationViewerAsync(cancellationToken);
        return Ok(await notifications.MarkAllReadAsync(viewer, cancellationToken));
    }
}
