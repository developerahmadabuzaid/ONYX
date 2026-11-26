using Onyx.API.Authentication;
using Onyx.Application.Customers;

namespace Onyx.API.Context;

/// <summary>The customer authenticated by the customer scheme on this request; null on every staff or anonymous request.</summary>
public sealed class HttpCurrentCustomer(IHttpContextAccessor httpContextAccessor) : ICurrentCustomer
{
    public Guid? CustomerId => Read(CustomerAuthentication.CustomerIdClaim);

    public Guid? SessionId => Read(CustomerAuthentication.SessionIdClaim);

    private Guid? Read(string claimType)
    {
        var identity = httpContextAccessor.HttpContext?.User.Identities
            .FirstOrDefault(candidate => candidate.IsAuthenticated && candidate.AuthenticationType == CustomerAuthentication.Scheme);
        return Guid.TryParse(identity?.FindFirst(claimType)?.Value, out var id) ? id : null;
    }
}
