using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Content;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Content;

namespace Onyx.API.Controllers.Admin;

/// <summary>Topic clusters and pillar pages (internal planning; not shown publicly except as related content).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/clusters")]
public sealed class AdminClustersController(IContentClusterService clusters) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<IReadOnlyList<ClusterResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClusterResponse>>> List(CancellationToken cancellationToken) => Ok(await clusters.ListAsync(cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType<ClusterResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Create(SaveClusterRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await clusters.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType<ClusterResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, SaveClusterRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await clusters.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await clusters.DeleteAsync(id, cancellationToken));

    [HttpPut("{id:guid}/members")]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType<ClusterResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetMember(Guid id, SetClusterMemberRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await clusters.SetMemberAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}/members/{contentItemId:guid}")]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType<ClusterResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid contentItemId, CancellationToken cancellationToken) =>
        this.OkOrProblem(await clusters.RemoveMemberAsync(id, contentItemId, cancellationToken));
}
