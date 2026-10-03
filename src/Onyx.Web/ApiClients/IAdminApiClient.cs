using Onyx.Contracts.Common;
using Onyx.Contracts.Identity;
using Onyx.Contracts.Notifications;

namespace Onyx.Web.ApiClients;

/// <summary>Calls made on behalf of the signed-in admin; the access token is attached server-side from the encrypted cookie.</summary>
public interface IAdminApiClient
{
    Task<ApiResult<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken);

    Task<ApiResult<int>> GetUnreadNotificationCountAsync(CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<AdminNotificationResponse>>> ListNotificationsAsync(int page, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken);
}
