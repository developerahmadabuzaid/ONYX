using System.Security.Claims;
using Onyx.Application.Abstractions;

namespace Onyx.API.Context;

/// <summary>Reads the authenticated admin from the request principal (JWT bearer authentication arrives in Stage 3).</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    // A customer principal (customer endpoints) is never a staff user.
    private ClaimsPrincipal? AuthenticatedPrincipal =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } principal
        && principal.Identity.AuthenticationType != Authentication.CustomerAuthentication.Scheme
            ? principal
            : null;

    public bool IsAuthenticated => AuthenticatedPrincipal is not null;

    public Guid? UserId =>
        AuthenticatedPrincipal is { } principal
        && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out var id)
            ? id
            : null;

    /// <summary>Access tokens carry no name (minimal claims); audit records identify users by id.</summary>
    public string? UserName => AuthenticatedPrincipal?.FindFirstValue("name");
}
