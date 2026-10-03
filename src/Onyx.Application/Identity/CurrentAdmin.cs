using Onyx.Application.Abstractions;
using Onyx.Application.Notifications;

namespace Onyx.Application.Identity;

public sealed class CurrentAdmin(ICurrentUser currentUser, IPermissionService permissions) : ICurrentAdmin
{
    public Task<ActorPermissions> GetPermissionsAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("An authenticated admin is required for this operation.");
        return permissions.GetAsync(userId, cancellationToken);
    }

    public async Task<NotificationViewer> GetNotificationViewerAsync(CancellationToken cancellationToken)
    {
        var actor = await GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        return new NotificationViewer(actor.UserId, actor.Permissions);
    }
}
