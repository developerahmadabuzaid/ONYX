using Microsoft.EntityFrameworkCore;
using Onyx.Application.Notifications;
using Onyx.Contracts.Common;
using Onyx.Contracts.Notifications;
using Onyx.Domain.Notifications;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository(ApplicationDbContext dbContext) : INotificationRepository
{
    public void Add(AdminNotification notification) => dbContext.AdminNotifications.Add(notification);

    public void AddReadState(NotificationReadState readState) => dbContext.NotificationReadStates.Add(readState);

    public async Task<PagedResult<AdminNotificationResponse>> ListVisibleAsync(
        NotificationViewer viewer, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Visible(viewer, unreadOnly);
        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var userId = viewer.UserId;

        var rows = await query
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(notification => new
            {
                notification.Id,
                notification.Type,
                notification.Severity,
                notification.Parameters,
                notification.ResourceType,
                notification.ResourceId,
                notification.CreatedAtUtc,
                IsRead = dbContext.NotificationReadStates.Any(state => state.NotificationId == notification.Id && state.UserId == userId),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(row => new AdminNotificationResponse(
                row.Id, row.Type.ToString(), row.Severity.ToString(), row.Parameters, row.ResourceType, row.ResourceId, row.CreatedAtUtc, row.IsRead))
            .ToList();

        return new PagedResult<AdminNotificationResponse>(items, page, pageSize, total);
    }

    public Task<int> CountUnreadAsync(NotificationViewer viewer, CancellationToken cancellationToken) =>
        Visible(viewer, unreadOnly: true).CountAsync(cancellationToken);

    public Task<bool> IsVisibleAsync(Guid notificationId, NotificationViewer viewer, CancellationToken cancellationToken) =>
        Visible(viewer, unreadOnly: false).AnyAsync(notification => notification.Id == notificationId, cancellationToken);

    public Task<bool> IsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.NotificationReadStates.AnyAsync(state => state.NotificationId == notificationId && state.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListUnreadVisibleIdsAsync(NotificationViewer viewer, CancellationToken cancellationToken) =>
        await Visible(viewer, unreadOnly: true).Select(notification => notification.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>The single visibility rule: the viewer must hold the notification's required permission.</summary>
    private IQueryable<AdminNotification> Visible(NotificationViewer viewer, bool unreadOnly)
    {
        var permissions = viewer.Permissions.ToArray();
        var userId = viewer.UserId;
        var query = dbContext.AdminNotifications.AsNoTracking().Where(notification => permissions.Contains(notification.RequiredPermission));

        return unreadOnly
            ? query.Where(notification => !dbContext.NotificationReadStates.Any(state => state.NotificationId == notification.Id && state.UserId == userId))
            : query;
    }
}
