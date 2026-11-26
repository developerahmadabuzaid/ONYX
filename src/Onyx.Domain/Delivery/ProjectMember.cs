using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>An admin user's membership of one delivery project. The user lives in Identity; only the id is stored here.</summary>
public sealed class ProjectMember : Entity
{
    private ProjectMember()
    {
    }

    internal ProjectMember(Guid projectId, Guid userId, ProjectRole role, DateTimeOffset addedAtUtc)
    {
        ProjectId = projectId;
        UserId = userId;
        Role = role;
        AddedAtUtc = addedAtUtc;
    }

    public Guid ProjectId { get; private set; }

    public Guid UserId { get; private set; }

    public ProjectRole Role { get; private set; }

    public DateTimeOffset AddedAtUtc { get; private set; }

    internal void ChangeRole(ProjectRole role) => Role = role;
}
