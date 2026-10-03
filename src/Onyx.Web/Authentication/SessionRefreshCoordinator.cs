using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Onyx.Contracts.Identity;
using Onyx.Web.ApiClients;

namespace Onyx.Web.Authentication;

/// <summary>
/// Collapses concurrent refreshes of the same session (parallel requests from one browser) into a single API call within this
/// instance. Across instances, the API's grace window returns the same successor token.
/// </summary>
public sealed class SessionRefreshCoordinator
{
    private readonly ConcurrentDictionary<string, Lazy<Task<ApiResult<TokenPairResponse>>>> _inFlight = new(StringComparer.Ordinal);

    public async Task<ApiResult<TokenPairResponse>> RefreshAsync(string refreshToken, Func<Task<ApiResult<TokenPairResponse>>> refresh)
    {
        ArgumentNullException.ThrowIfNull(refresh);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        var call = _inFlight.GetOrAdd(key, _ => new Lazy<Task<ApiResult<TokenPairResponse>>>(refresh));

        try
        {
            return await call.Value.ConfigureAwait(false);
        }
        finally
        {
            _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<ApiResult<TokenPairResponse>>>>(key, call));
        }
    }
}
