using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>
/// A topic cluster: a pillar page plus the supporting content that covers the topic. Internal (not shown publicly); used to
/// plan coverage, find gaps and guide internal linking. Content need not belong to any cluster.
/// </summary>
public sealed class ContentCluster : AuditableEntity, IAuditable
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2_000;

    private readonly List<ContentClusterMember> _members = [];

    private ContentCluster()
    {
        Name = string.Empty;
    }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyList<ContentClusterMember> Members => _members;

    public Guid? PillarContentItemId => _members.FirstOrDefault(member => member.Role == ClusterRole.Pillar)?.ContentItemId;

    public static Result<ContentCluster> Create(string name, string? description)
    {
        var cluster = new ContentCluster { IsActive = true };
        var updated = cluster.Update(name, description, isActive: true);
        return updated.IsSuccess ? cluster : updated.Error!;
    }

    public Result Update(string name, string? description, bool isActive)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > NameMaxLength || description is { Length: > DescriptionMaxLength })
        {
            return Error.Validation("Cluster.Invalid", $"A name of 1-{NameMaxLength} characters is required; descriptions are limited to {DescriptionMaxLength}.");
        }

        Name = trimmed;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = isActive;
        return Result.Success();
    }

    /// <summary>Adds or changes a member. A cluster has at most one pillar page.</summary>
    public Result SetMember(Guid contentItemId, ClusterRole role)
    {
        if (!Enum.IsDefined(role))
        {
            return Error.Validation("Cluster.InvalidRole", "Unknown cluster role.");
        }

        if (role == ClusterRole.Pillar && _members.Any(member => member.Role == ClusterRole.Pillar && member.ContentItemId != contentItemId))
        {
            return Error.Conflict("Cluster.PillarExists", "This cluster already has a pillar page. Change it to supporting first.");
        }

        var existing = _members.FirstOrDefault(member => member.ContentItemId == contentItemId);
        if (existing is null)
        {
            _members.Add(ContentClusterMember.Create(Id, contentItemId, role));
        }
        else
        {
            existing.ChangeRole(role);
        }

        return Result.Success();
    }

    public bool RemoveMember(Guid contentItemId) => _members.RemoveAll(member => member.ContentItemId == contentItemId) > 0;
}
