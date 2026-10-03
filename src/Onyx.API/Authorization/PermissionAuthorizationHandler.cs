using Microsoft.AspNetCore.Authorization;
using Onyx.Application.Abstractions;
using Onyx.Application.Identity;

namespace Onyx.API.Authorization;

/// <summary>The single enforcement point for admin permissions.</summary>
public sealed class PermissionAuthorizationHandler(ICurrentUser currentUser, IPermissionService permissions)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (currentUser.UserId is not { } userId)
        {
            return;
        }

        var actor = await permissions.GetAsync(userId, CancellationToken.None).ConfigureAwait(false);
        if (actor.Permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
