using Microsoft.AspNetCore.Authorization;

namespace Onyx.API.Authorization;

/// <summary>
/// Fallback policy: every matched endpoint requires an authenticated admin unless it opts out with [AllowAnonymous].
/// Requests that match no endpoint pass through so they get a normal 404.
/// </summary>
public sealed class AuthenticatedEndpointRequirement : AuthorizationHandler<AuthenticatedEndpointRequirement>, IAuthorizationRequirement
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AuthenticatedEndpointRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        var noEndpoint = context.Resource is HttpContext httpContext && httpContext.GetEndpoint() is null;
        if (noEndpoint || context.User.Identity?.IsAuthenticated == true)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
