using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Proof;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Proof;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// Capabilities, metrics and testimonials of projects and case studies. The application layer enforces the page's content-type
/// scope and, for testimonials, the matching <c>Testimonials.*</c> permission; publishing other items needs <c>Content.Publish</c>.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/proof-items")]
public sealed class AdminProofItemsController(IProofItemService proofItems) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<IReadOnlyList<ProofItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] Guid contentItemId, CancellationToken cancellationToken) =>
        this.OkOrProblem(await proofItems.ListAsync(contentItemId, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Content.Edit)]
    [ProducesResponseType<ProofItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create(SaveProofItemRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await proofItems.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Content.Edit)]
    [ProducesResponseType<ProofItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update(Guid id, SaveProofItemRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await proofItems.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/verify")]
    [HasPermission(Permissions.Proof.Verify)]
    [ProducesResponseType<ProofItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Verify(Guid id, VerifyProofRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await proofItems.VerifyAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/publish")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<ProofItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Publish(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await proofItems.PublishAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/unpublish")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<ProofItemResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Unpublish(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await proofItems.UnpublishAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Content.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await proofItems.DeleteAsync(id, cancellationToken));
}
