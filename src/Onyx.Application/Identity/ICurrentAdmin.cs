using Onyx.Application.Notifications;

namespace Onyx.Application.Identity;

/// <summary>The authenticated admin with server-resolved permissions.</summary>
public interface ICurrentAdmin
{
    Task<ActorPermissions> GetPermissionsAsync(CancellationToken cancellationToken);

    Task<NotificationViewer> GetNotificationViewerAsync(CancellationToken cancellationToken);
}
