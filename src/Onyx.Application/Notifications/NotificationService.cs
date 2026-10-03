using Onyx.Application.Abstractions.Persistence;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Notifications;
using Onyx.Domain.Common;
using Onyx.Domain.Notifications;

namespace Onyx.Application.Notifications;

public sealed class NotificationService(INotificationRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : INotificationService
{
    public const int MaxPageSize = 100;

    private static readonly Error UnknownPermission =
        Error.Validation("Notification.UnknownPermission", "Notifications must require a permission from the catalogue.");

    private static readonly Error NotFound =
        Error.NotFound("Notification.NotFound", "Notification not found.");

    public async Task<Result<Guid>> PublishAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A typo'd permission would make the notification invisible to everyone (or worse, to the wrong people).
        if (!Permissions.All.Contains(request.RequiredPermission))
        {
            return UnknownPermission;
        }

        var notification = AdminNotification.Create(
            timeProvider.GetUtcNow(),
            request.Type,
            request.Severity,
            request.RequiredPermission,
            request.Parameters,
            request.ResourceType,
            request.ResourceId);

        repository.Add(notification);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return notification.Id;
    }

    public Task<PagedResult<AdminNotificationResponse>> ListAsync(
        NotificationViewer viewer, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        return repository.ListVisibleAsync(viewer, unreadOnly, Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), cancellationToken);
    }

    public Task<int> CountUnreadAsync(NotificationViewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        return repository.CountUnreadAsync(viewer, cancellationToken);
    }

    public async Task<Result> MarkReadAsync(NotificationViewer viewer, Guid notificationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        if (!await repository.IsVisibleAsync(notificationId, viewer, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(NotFound);
        }

        if (!await repository.IsReadAsync(notificationId, viewer.UserId, cancellationToken).ConfigureAwait(false))
        {
            repository.AddReadState(NotificationReadState.Create(notificationId, viewer.UserId, timeProvider.GetUtcNow()));
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<int> MarkAllReadAsync(NotificationViewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var unread = await repository.ListUnreadVisibleIdsAsync(viewer, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        foreach (var notificationId in unread)
        {
            repository.AddReadState(NotificationReadState.Create(notificationId, viewer.UserId, now));
        }

        if (unread.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return unread.Count;
    }
}
