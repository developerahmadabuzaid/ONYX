using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Onyx.Web.Authentication;

namespace Onyx.Web.ApiClients.Handlers;

/// <summary>
/// Attaches the signed-in admin's access token, read server-side from the encrypted authentication cookie. A 401 from the
/// API means the session was revoked or expired: <see cref="AdminSessionEndedException"/> lets the Admin area sign out.
/// </summary>
public sealed class AccessTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var httpContext = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("Admin API calls require an HTTP request.");

        var accessToken = await httpContext.GetTokenAsync(BffDefaults.Scheme, BffDefaults.AccessTokenName).ConfigureAwait(false);
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new AdminSessionEndedException();
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            throw new AdminSessionEndedException();
        }

        return response;
    }
}
