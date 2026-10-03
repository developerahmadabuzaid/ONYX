using System.Globalization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Identity;
using Onyx.Contracts.Notifications;

namespace Onyx.Web.ApiClients;

public sealed class AdminApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IAdminApiClient
{
    public Task<ApiResult<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        GetAsync<CurrentUserResponse>("api/v1/auth/me", cancellationToken);

    public Task<ApiResult<NoContent>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>("api/v1/auth/password/change", request, cancellationToken);

    public Task<ApiResult<int>> GetUnreadNotificationCountAsync(CancellationToken cancellationToken) =>
        GetAsync<int>("api/v1/admin/notifications/unread-count", cancellationToken);

    public Task<ApiResult<PagedResult<AdminNotificationResponse>>> ListNotificationsAsync(int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<AdminNotificationResponse>>(string.Create(CultureInfo.InvariantCulture, $"api/v1/admin/notifications?page={page}&pageSize=20"), cancellationToken);

    public Task<ApiResult<NoContent>> MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"api/v1/admin/notifications/{notificationId:D}/read", null, cancellationToken);
}
