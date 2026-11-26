using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Delivery;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

/// <summary>
/// Delivery projects: list, details, create (workspace administrators), edit and membership (project admins). Every operation
/// resolves the caller's capability in the project on the server.
/// </summary>
public sealed class DeliveryProjectService(
    IDeliveryProjectRepository projects,
    IDeliveryReadModel readModel,
    DeliveryAccess access,
    IUserDirectory users,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<DeliveryProjectListItemResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        return await readModel.ListProjectsAsync(viewer, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DeliveryProjectResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        var project = await projects.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return DeliveryAccess.ProjectNotFound;
        }

        var can = DeliveryAccess.For(viewer, project);
        return can.IsFailure ? can.Error! : await ToResponseAsync(project, viewer, can.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Users who can be made members: they hold <c>Delivery.Access</c> (SuperAdmins hold every permission).</summary>
    public async Task<IReadOnlyList<DeliveryUserResponse>> EligibleUsersAsync(CancellationToken cancellationToken) =>
        (await users.ListWithPermissionAsync(Permissions.Delivery.Access, cancellationToken).ConfigureAwait(false))
            .Select(user => new DeliveryUserResponse(user.UserId, user.DisplayName))
            .OrderBy(user => user.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public async Task<Result<DeliveryProjectResponse>> CreateAsync(SaveDeliveryProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        if (!viewer.IsAdministrator)
        {
            return Error.Forbidden("Delivery.CreateProjectForbidden", "Only workspace administrators create projects.");
        }

        var details = ParseDetails(request);
        if (details.IsFailure)
        {
            return details.Error!;
        }

        var key = request.Key?.Trim().ToUpperInvariant() ?? string.Empty;
        if (DeliveryProject.IsValidKey(key) && await projects.KeyExistsAsync(key, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Delivery.ProjectKeyTaken", $"Another project already uses the key {key}.");
        }

        var created = DeliveryProject.Create(key, viewer.UserId, details.Value, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error!;
        }

        projects.Add(created.Value);
        auditTrail.Record(AuditAction.DeliveryProjectCreated, AuditCategory.Delivery, nameof(DeliveryProject), created.Value.Id.ToString(), new { key = created.Value.Key });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(created.Value, viewer, DeliveryAccess.Capabilities(viewer, ProjectRole.Admin), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DeliveryProjectResponse>> UpdateAsync(Guid id, SaveDeliveryProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is not { } version)
        {
            return Error.Validation("Delivery.VersionRequired", "Reload the project and try again.");
        }

        var (project, viewer, can, error) = await LoadForManagementAsync(id, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        var details = ParseDetails(request);
        if (details.IsFailure)
        {
            return details.Error!;
        }

        projects.SetExpectedVersion(project!, version);
        var updated = project!.Update(details.Value);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        auditTrail.Record(AuditAction.DeliveryProjectUpdated, AuditCategory.Delivery, nameof(DeliveryProject), project.Id.ToString(), new { key = project.Key });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(project, viewer!, can!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DeliveryProjectResponse>> SaveMemberAsync(Guid id, SaveProjectMemberRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (project, viewer, can, error) = await LoadForManagementAsync(id, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        var role = DeliveryEnums.Parse<ProjectRole>(request.Role, "Role");
        if (role.IsFailure)
        {
            return role.Error!;
        }

        projects.SetExpectedVersion(project!, request.Version);
        Result changed;
        if (project!.RoleOf(request.UserId) is null)
        {
            var eligible = await users.ListWithPermissionAsync(Permissions.Delivery.Access, cancellationToken).ConfigureAwait(false);
            if (eligible.All(user => user.UserId != request.UserId))
            {
                return Error.Validation("Delivery.UserNotEligible", "Only active users with access to the delivery workspace can join a project.");
            }

            changed = project.AddMember(request.UserId, role.Value, timeProvider.GetUtcNow());
        }
        else
        {
            changed = project.ChangeMemberRole(request.UserId, role.Value);
        }

        if (changed.IsFailure)
        {
            return changed.Error!;
        }

        auditTrail.Record(AuditAction.DeliveryMembersChanged, AuditCategory.Delivery, nameof(DeliveryProject), project.Id.ToString(), new { userId = request.UserId, role = role.Value.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(project, viewer!, can!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DeliveryProjectResponse>> RemoveMemberAsync(Guid id, Guid userId, uint version, CancellationToken cancellationToken)
    {
        var (project, viewer, can, error) = await LoadForManagementAsync(id, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        projects.SetExpectedVersion(project!, version);
        var removed = project!.RemoveMember(userId);
        if (removed.IsFailure)
        {
            return removed.Error!;
        }

        auditTrail.Record(AuditAction.DeliveryMembersChanged, AuditCategory.Delivery, nameof(DeliveryProject), project.Id.ToString(), new { userId, removed = true });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(project, viewer!, can!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DeliveryProjectResponse>> SetWipLimitAsync(Guid id, Guid statusId, SetWipLimitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (project, viewer, can, error) = await LoadForManagementAsync(id, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        projects.SetExpectedVersion(project!, request.Version);
        var set = project!.SetWipLimit(statusId, request.WipLimit);
        if (set.IsFailure)
        {
            return set.Error!;
        }

        // The limit is an advisory setting on a status row (last write wins); the board only warns when a column exceeds it.
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(project, viewer!, can!, cancellationToken).ConfigureAwait(false);
    }

    internal static DeliveryProjectResponse ToResponse(
        DeliveryProject project, DeliveryViewer viewer, DeliveryCapabilities can, IReadOnlyDictionary<Guid, string> names, int total, int done) =>
        new(
            project.Id,
            project.Key,
            project.Name,
            project.Description,
            project.Type.ToString(),
            project.Classification.ToString(),
            project.ClientName,
            project.Status.ToString(),
            project.Priority.ToString(),
            project.OwnerId,
            names.GetValueOrDefault(project.OwnerId, string.Empty),
            project.StartDate,
            project.TargetDate,
            project.RepositoryUrl,
            project.RoleOf(viewer.UserId)?.ToString(),
            can,
            Statuses(project),
            project.Transitions.Select(transition => new WorkflowTransitionResponse(transition.FromStatusId, transition.ToStatusId)).ToList(),
            project.Members
                .Select(member => new ProjectMemberResponse(member.UserId, names.GetValueOrDefault(member.UserId, string.Empty), member.Role.ToString()))
                .OrderByDescending(member => Enum.Parse<ProjectRole>(member.Role))
                .ThenBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            total,
            done,
            project.Version);

    internal static IReadOnlyList<WorkflowStatusResponse> Statuses(DeliveryProject project) =>
        project.Statuses.OrderBy(status => status.Order).Select(ToResponse).ToList();

    internal static WorkflowStatusResponse ToResponse(WorkflowStatus status) =>
        new(status.Id, status.Name, status.SystemKey, status.Category.ToString(), status.Order, status.WipLimit);

    private async Task<DeliveryProjectResponse> ToResponseAsync(DeliveryProject project, DeliveryViewer viewer, DeliveryCapabilities can, CancellationToken cancellationToken)
    {
        var ids = project.Members.Select(member => member.UserId).Append(project.OwnerId).Distinct().ToList();
        var names = await readModel.DisplayNamesAsync(ids, cancellationToken).ConfigureAwait(false);
        var (total, done) = await readModel.ProjectIssueCountsAsync(project.Id, cancellationToken).ConfigureAwait(false);
        return ToResponse(project, viewer, can, names, total, done);
    }

    private async Task<(DeliveryProject? Project, DeliveryViewer? Viewer, DeliveryCapabilities? Can, Error? Error)> LoadForManagementAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        var project = await projects.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return (null, null, null, DeliveryAccess.ProjectNotFound);
        }

        var can = DeliveryAccess.For(viewer, project);
        if (can.IsFailure)
        {
            return (null, null, null, can.Error);
        }

        return can.Value.ManageProject ? (project, viewer, can.Value, null) : (null, null, null, DeliveryAccess.Forbidden);
    }

    private static Result<DeliveryProjectDetails> ParseDetails(SaveDeliveryProjectRequest request)
    {
        var type = DeliveryEnums.Parse<DeliveryProjectType>(request.Type, "Type");
        var classification = DeliveryEnums.Parse<DeliveryClassification>(request.Classification, "Classification");
        var status = DeliveryEnums.ParseOrDefault(request.Status, "Status", DeliveryProjectStatus.Active);
        var priority = DeliveryEnums.ParseOrDefault(request.Priority, "Priority", DeliveryPriority.Medium);
        foreach (var check in new Result[] { type, classification, status, priority })
        {
            if (check.IsFailure)
            {
                return check.Error!;
            }
        }

        return new DeliveryProjectDetails(
            request.Name, request.Description, type.Value, classification.Value, request.ClientName, status.Value, priority.Value,
            request.StartDate, request.TargetDate, request.RepositoryUrl);
    }
}
