using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Onyx.Application.Customers;
using Onyx.Contracts.Customers;

namespace Onyx.API.Authentication;

/// <summary>
/// Authenticates <c>Authorization: OnyxCustomer {token}</c> by checking the opaque session token against the database on every
/// request (revocation is immediate). Staff <c>Bearer</c> tokens are ignored here, as customer tokens are ignored by the staff
/// JWT handler. The principal carries only Onyx customer claims, never <c>sub</c>, so it can never pass as a staff user.
/// </summary>
public sealed class CustomerSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ICustomerAuthService customers) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header)
            || !string.Equals(header.Scheme, CustomerAuthDefaults.AuthorizationScheme, StringComparison.Ordinal)
            || string.IsNullOrEmpty(header.Parameter))
        {
            return AuthenticateResult.NoResult();
        }

        var session = await customers.ValidateSessionAsync(header.Parameter, Context.RequestAborted).ConfigureAwait(false);
        if (session is null)
        {
            return AuthenticateResult.Fail("The customer session is not valid.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(CustomerAuthentication.CustomerIdClaim, session.CustomerId.ToString()),
                new Claim(CustomerAuthentication.SessionIdClaim, session.SessionId.ToString()),
            ],
            CustomerAuthentication.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), CustomerAuthentication.Scheme));
    }
}
