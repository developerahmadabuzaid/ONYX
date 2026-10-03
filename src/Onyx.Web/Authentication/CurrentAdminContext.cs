using Onyx.Contracts.Identity;
using Onyx.Web.ApiClients;

namespace Onyx.Web.Authentication;

/// <summary>
/// The signed-in admin's profile and permissions, fetched from the API once per request. Used only to hide UI the admin
/// cannot use; the API enforces every permission itself.
/// </summary>
public sealed class CurrentAdminContext(IAdminApiClient adminApi)
{
    private CurrentUserResponse? _current;

    public async Task<CurrentUserResponse> GetAsync(CancellationToken cancellationToken)
    {
        if (_current is not null)
        {
            return _current;
        }

        var result = await adminApi.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        _current = result.IsSuccess && result.Value is not null ? result.Value : throw new AdminSessionEndedException();
        return _current;
    }

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken) =>
        (await GetAsync(cancellationToken).ConfigureAwait(false)).Permissions.Contains(permission);
}
