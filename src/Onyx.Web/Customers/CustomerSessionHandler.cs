using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Onyx.Contracts.Customers;

namespace Onyx.Web.Customers;

/// <summary>
/// Attaches the signed-in customer's session token (read server-side from the encrypted customer cookie) as
/// <c>Authorization: OnyxCustomer …</c>. A 401 from the API ends the customer's sign-in here too.
/// </summary>
public sealed class CustomerSessionHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var httpContext = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("Customer API calls require an HTTP request.");
        var token = await httpContext.GetTokenAsync(CustomerWebAuth.Scheme, CustomerWebAuth.SessionTokenName).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
        {
            throw new CustomerSessionEndedException();
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(CustomerAuthDefaults.AuthorizationScheme, token);
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            throw new CustomerSessionEndedException();
        }

        return response;
    }
}
