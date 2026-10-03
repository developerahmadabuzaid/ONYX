using Onyx.Contracts.Common;
using Onyx.Contracts.Notifications;
using Onyx.Domain.Common;

namespace Onyx.Application.Notifications;

public interface INotificationService
{
    /// <summary>Creates and persists a notification immediately.</summary>
    Task<Result<Guid>> PublishAsync(NotificationRequest request, CancellationToken cancellationToken);

    Task<PagedResult<AdminNotificationResponse>> ListAsync(
        NotificationViewer viewer, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(NotificationViewer viewer, CancellationToken cancellationToken);

    /// <summary>Returns NotFound for notifications the viewer may not see, so their existence is not revealed.</summary>
    Task<Result> MarkReadAsync(NotificationViewer viewer, Guid notificationId, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(NotificationViewer viewer, CancellationToken cancellationToken);
}
