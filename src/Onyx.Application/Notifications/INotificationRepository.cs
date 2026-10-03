using Onyx.Contracts.Common;
using Onyx.Contracts.Notifications;
using Onyx.Domain.Notifications;

namespace Onyx.Application.Notifications;

public interface INotificationRepository
{
    void Add(AdminNotification notification);

    Task<PagedResult<AdminNotificationResponse>> ListVisibleAsync(
        NotificationViewer viewer, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(NotificationViewer viewer, CancellationToken cancellationToken);

    Task<bool> IsVisibleAsync(Guid notificationId, NotificationViewer viewer, CancellationToken cancellationToken);

    Task<bool> IsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken);

    void AddReadState(NotificationReadState readState);

    Task<IReadOnlyList<Guid>> ListUnreadVisibleIdsAsync(NotificationViewer viewer, CancellationToken cancellationToken);
}
