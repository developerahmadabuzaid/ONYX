using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Proof;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// Proof verification of projects, case studies and integrations (<c>Proof.Verify</c>) and the homepage's featured projects
/// (<c>Projects.Publish</c>). The application layer additionally enforces the content-type scope.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/content/{id:guid}")]
public sealed class AdminContentProofController(IContentProofService proof) : ControllerBase
{
    [HttpPost("proof-verification")]
    [HasPermission(Permissions.Proof.Verify)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Verify(Guid id, VerifyProofRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await proof.VerifyAsync(id, request, cancellationToken));

    [HttpPost("proof-verification/revoke")]
    [HasPermission(Permissions.Proof.Verify)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Revoke(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await proof.RevokeAsync(id, request, cancellationToken));

    [HttpPost("featured")]
    [HasPermission(Permissions.Projects.Publish)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SetFeatured(Guid id, SetFeaturedRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await proof.SetFeaturedAsync(id, request, cancellationToken));
}
