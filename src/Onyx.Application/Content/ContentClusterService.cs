using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Common;
using Onyx.Contracts.Content;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>Topic clusters with coverage observations derived from the graph itself (no performance data is assumed).</summary>
public sealed class ContentClusterService(IContentClusterRepository clusters, IContentRepository content, IUnitOfWork unitOfWork) : IContentClusterService
{
    private static readonly Error NotFound = Error.NotFound("Cluster.NotFound", "Cluster not found.");

    public async Task<IReadOnlyList<ClusterResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var list = await clusters.ListAsync(cancellationToken).ConfigureAwait(false);
        var items = await content.GetLookupAsync(list.SelectMany(cluster => cluster.Members).Select(member => member.ContentItemId).Distinct().ToList(), cancellationToken)
            .ConfigureAwait(false);
        return list.Select(cluster => ToResponse(cluster, items)).ToList();
    }

    public async Task<Result<ClusterResponse>> CreateAsync(SaveClusterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var created = ContentCluster.Create(request.Name, request.Description);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        if (!request.IsActive)
        {
            created.Value.Update(request.Name, request.Description, isActive: false);
        }

        clusters.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(created.Value, new Dictionary<Guid, ContentItem>());
    }

    public async Task<Result<ClusterResponse>> UpdateAsync(Guid id, SaveClusterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await ChangeAsync(id, request.Version, cluster => cluster.Update(request.Name, request.Description, request.IsActive), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var cluster = await clusters.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (cluster is null)
        {
            return NotFound;
        }

        clusters.Remove(cluster);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<ClusterResponse>> SetMemberAsync(Guid id, SetClusterMemberRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<ClusterRole>(request.Role, out var role))
        {
            return Error.Validation("Cluster.InvalidRole", "Unknown cluster role.");
        }

        if (!(await content.GetLookupAsync([request.ContentItemId], cancellationToken).ConfigureAwait(false)).ContainsKey(request.ContentItemId))
        {
            return ContentErrors.NotFound;
        }

        return await ChangeAsync(id, null, cluster => cluster.SetMember(request.ContentItemId, role), cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<ClusterResponse>> RemoveMemberAsync(Guid id, Guid contentItemId, CancellationToken cancellationToken) =>
        ChangeAsync(id, null, cluster => cluster.RemoveMember(contentItemId) ? Result.Success() : Error.NotFound("Cluster.MemberNotFound", "Not a member."), cancellationToken);

    public static IReadOnlyList<string> Observe(ContentCluster cluster, IReadOnlyDictionary<Guid, ContentItem> items)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        ArgumentNullException.ThrowIfNull(items);
        var observations = new List<string>();
        if (cluster.PillarContentItemId is not { } pillar)
        {
            observations.Add("No pillar page is assigned.");
        }
        else if (!items.TryGetValue(pillar, out var pillarItem) || !pillarItem.IsLive)
        {
            observations.Add("The pillar page is not live.");
        }

        var supporting = cluster.Members.Where(member => member.Role == ClusterRole.Supporting).ToList();
        if (supporting.Count == 0)
        {
            observations.Add("No supporting content yet: a content gap for this topic.");
        }

        var offline = supporting.Count(member => !items.TryGetValue(member.ContentItemId, out var item) || !item.IsLive);
        if (offline > 0)
        {
            observations.Add($"{offline} supporting page(s) are not live.");
        }

        return observations;
    }

    private async Task<Result<ClusterResponse>> ChangeAsync(Guid id, uint? version, Func<ContentCluster, Result> change, CancellationToken cancellationToken)
    {
        var cluster = await clusters.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (cluster is null)
        {
            return NotFound;
        }

        if (version is { } expected)
        {
            clusters.SetExpectedVersion(cluster, expected);
        }

        var result = change(cluster);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var items = await content.GetLookupAsync(cluster.Members.Select(member => member.ContentItemId).ToList(), cancellationToken).ConfigureAwait(false);
        return ToResponse(cluster, items);
    }

    private static ClusterResponse ToResponse(ContentCluster cluster, IReadOnlyDictionary<Guid, ContentItem> items) => new(
        cluster.Id,
        cluster.Name,
        cluster.Description,
        cluster.IsActive,
        cluster.Members.Select(member => new ClusterMemberResponse(
            member.ContentItemId,
            member.Role.ToString(),
            items.TryGetValue(member.ContentItemId, out var item) ? ContentMapping.Titles(item) : new Dictionary<string, string>(),
            items.TryGetValue(member.ContentItemId, out var live) && live.IsLive)).ToList(),
        Observe(cluster, items),
        cluster.Version);
}
