using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Proof;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Models;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Capabilities, metrics and testimonials of a project or case study. Every change returns an item to an unverified draft; the API
/// enforces content-type scope, testimonial permissions, proof verification and publishing rights.
/// </summary>
[Route("admin/content/{contentItemId:guid}/proof")]
public sealed class ProofItemsController(IAdminCmsApiClient cms) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid contentItemId, CancellationToken cancellationToken)
    {
        var content = await cms.GetContentAsync(contentItemId, cancellationToken);
        if (Failed(content) is { } failure)
        {
            return failure;
        }

        var items = await cms.ListProofItemsAsync(contentItemId, cancellationToken);
        return Failed(items) ?? View(new ProofItemsViewModel(content.Value!, items.Value!));
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(Guid contentItemId, ProofItemForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var result = await cms.CreateProofItemAsync(form.ToRequest(contentItemId, null), cancellationToken);
        return After(result, RedirectToAction(nameof(Index), new { contentItemId }), "Cms.Created");
    }

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Update(Guid contentItemId, Guid id, ProofItemForm form, uint version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var result = await cms.UpdateProofItemAsync(id, form.ToRequest(contentItemId, version), cancellationToken);
        return After(result, RedirectToAction(nameof(Index), new { contentItemId }));
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid contentItemId, Guid id, string sourceNote, string clientPermission, string? clientPermissionNote, uint version, CancellationToken cancellationToken)
    {
        var result = await cms.VerifyProofItemAsync(id, new VerifyProofRequest(sourceNote ?? string.Empty, clientPermission, Blank(clientPermissionNote), version), cancellationToken);
        return After(result, RedirectToAction(nameof(Index), new { contentItemId }), "Cms.Proof.Verified");
    }

    [HttpPost("{id:guid}/action")]
    public async Task<IActionResult> Transition(Guid contentItemId, Guid id, string operation, uint version, CancellationToken cancellationToken)
    {
        if (operation is not ("publish" or "unpublish"))
        {
            return ShowError("Cms.InvalidAction", RedirectToAction(nameof(Index), new { contentItemId }));
        }

        var result = await cms.ProofItemActionAsync(id, operation, version, cancellationToken);
        return After(result, RedirectToAction(nameof(Index), new { contentItemId }), "Cms.Done");
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid contentItemId, Guid id, CancellationToken cancellationToken)
    {
        var result = await cms.DeleteProofItemAsync(id, cancellationToken);
        return After(result, RedirectToAction(nameof(Index), new { contentItemId }), "Cms.Deleted");
    }
}
