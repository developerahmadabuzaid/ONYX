using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Content;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>Topic clusters: a pillar page plus supporting content. Observations are structural checks, not performance claims.</summary>
[Route("admin/clusters")]
public sealed class ClustersController(IAdminCmsApiClient cms) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await cms.ListClustersAsync(cancellationToken);
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(string name, string? description, CancellationToken cancellationToken)
    {
        var result = await cms.CreateClusterAsync(new SaveClusterRequest(name ?? string.Empty, Blank(description), true, null), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> SetMember(Guid id, Guid contentItemId, string role, CancellationToken cancellationToken)
    {
        var result = await cms.SetClusterMemberAsync(id, new SetClusterMemberRequest(contentItemId, role), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)));
    }

    [HttpPost("{id:guid}/members/{contentItemId:guid}/delete")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid contentItemId, CancellationToken cancellationToken)
    {
        var result = await cms.RemoveClusterMemberAsync(id, contentItemId, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Deleted");
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await cms.DeleteClusterAsync(id, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Deleted");
    }
}
