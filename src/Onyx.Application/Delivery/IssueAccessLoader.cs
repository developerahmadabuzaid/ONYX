using Onyx.Contracts.Delivery;
using Onyx.Domain.Common;

namespace Onyx.Application.Delivery;

/// <summary>
/// Loads an issue with its project and the caller's capabilities in it. Issues of projects the caller cannot see are reported as
/// not found; a visible issue the caller's role does not allow acting on is forbidden.
/// </summary>
public sealed class IssueAccessLoader(IDeliveryProjectRepository projects, IIssueRepository issues, DeliveryAccess access)
{
    public async Task<Result<LoadedIssue>> LoadAsync(Guid issueId, Func<DeliveryCapabilities, bool> capability, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capability);
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        var issue = await issues.GetAsync(issueId, cancellationToken).ConfigureAwait(false);
        if (issue is null)
        {
            return DeliveryAccess.IssueNotFound;
        }

        var project = await projects.GetAsync(issue.ProjectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return DeliveryAccess.IssueNotFound;
        }

        var can = DeliveryAccess.For(viewer, project);
        if (can.IsFailure)
        {
            return DeliveryAccess.IssueNotFound;
        }

        return capability(can.Value) ? new LoadedIssue(issue, project, viewer, can.Value) : DeliveryAccess.Forbidden;
    }
}
