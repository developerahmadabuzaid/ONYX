using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Intelligence;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Intelligence;

namespace Onyx.API.Controllers.Admin;

/// <summary>SEO recommendations with evidence. Approval (Seo.Approve) and application (Seo.Edit) are separate; nothing auto-applies.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/seo-recommendations")]
public sealed class AdminSeoRecommendationsController(ISeoRecommendationService recommendations) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<PagedResult<RecommendationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List(
        [FromQuery] string? status, [FromQuery] Guid? contentItemId, [FromQuery] string? kind, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        this.OkOrProblem(await recommendations.ListAsync(status, contentItemId, kind, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<RecommendationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) => this.OkOrProblem(await recommendations.GetAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType<RecommendationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Propose(ProposeRecommendationRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await recommendations.ProposeAsync(request, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    [HasPermission(Permissions.Seo.Approve)]
    [ProducesResponseType<RecommendationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Approve(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await recommendations.ApproveAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [HasPermission(Permissions.Seo.Approve)]
    [ProducesResponseType<RecommendationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Reject(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await recommendations.RejectAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/apply")]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType<RecommendationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Apply(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await recommendations.ApplyAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/mark-applied")]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType<RecommendationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> MarkApplied(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await recommendations.MarkAppliedAsync(id, request, cancellationToken));
}
