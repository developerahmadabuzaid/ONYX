using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Delivery;
using Onyx.Domain.Common;
using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

/// <summary>
/// Resource-level authorization for the delivery workspace. The API already requires <c>Delivery.Access</c>; this decides what the
/// caller may do in one project from their project role (Viewer &lt; Contributor &lt; Developer &lt; Manager &lt; Admin) or from the
/// workspace-wide <c>Delivery.Administer</c> permission. Projects the caller cannot see are reported as not found, so their
/// existence is not revealed.
/// </summary>
public sealed class DeliveryAccess(ICurrentAdmin currentAdmin)
{
    public static readonly Error ProjectNotFound = Error.NotFound("Delivery.ProjectNotFound", "Project not found.");

    public static readonly Error IssueNotFound = Error.NotFound("Delivery.IssueNotFound", "Issue not found.");

    public static readonly Error Forbidden = Error.Forbidden("Delivery.Forbidden", "Your role in this project does not allow this action.");

    public async Task<DeliveryViewer> ViewerAsync(CancellationToken cancellationToken)
    {
        var actor = await currentAdmin.GetPermissionsAsync(cancellationToken).ConfigureAwait(false);
        return new DeliveryViewer(actor.UserId, actor.IsSuperAdmin || actor.Permissions.Contains(Permissions.Delivery.Administer));
    }

    public static DeliveryCapabilities Capabilities(DeliveryViewer viewer, ProjectRole? role)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        if (viewer.IsAdministrator)
        {
            return new DeliveryCapabilities(true, true, true, true, true, true);
        }

        if (role is not { } known)
        {
            return new DeliveryCapabilities(false, false, false, false, false, false);
        }

        return new DeliveryCapabilities(
            View: true,
            CreateIssues: known >= ProjectRole.Contributor,
            Comment: known >= ProjectRole.Contributor,
            EditIssues: known >= ProjectRole.Developer,
            ManageSprints: known >= ProjectRole.Manager,
            ManageProject: known >= ProjectRole.Admin);
    }

    /// <summary>The caller's capabilities in <paramref name="project"/>, or not found when they cannot see it.</summary>
    public static Result<DeliveryCapabilities> For(DeliveryViewer viewer, DeliveryProject project)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(project);
        var capabilities = Capabilities(viewer, project.RoleOf(viewer.UserId));
        return capabilities.View ? capabilities : ProjectNotFound;
    }
}
