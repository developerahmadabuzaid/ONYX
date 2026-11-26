using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Delivery;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// Delivery analytics, roadmap, sprint reports and retrospectives, and the caller's saved issue filters. Everything is limited to
/// the projects the caller can see; retrospectives need the Manager role (or <c>Delivery.Administer</c>).
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/delivery")]
[HasPermission(Permissions.Delivery.Access)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDeliveryInsightsController(
    DeliveryQueryService queries,
    SprintService sprints,
    SavedIssueFilterService filters) : ControllerBase
{
    /// <summary>Analytics for the last <paramref name="range"/> days (7, 30 or 90), for every visible project or one.</summary>
    [HttpGet("analytics")]
    [ProducesResponseType<DeliveryAnalyticsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Analytics([FromQuery] int range = 30, [FromQuery] Guid? projectId = null, CancellationToken cancellationToken = default) =>
        this.OkOrProblem(await queries.AnalyticsAsync(range, projectId, cancellationToken));

    [HttpGet("roadmap")]
    [ProducesResponseType<RoadmapResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RoadmapResponse>> Roadmap(CancellationToken cancellationToken) =>
        Ok(await queries.RoadmapAsync(cancellationToken));

    [HttpGet("sprints/{id:guid}/report")]
    [ProducesResponseType<SprintReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SprintReport(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await sprints.ReportAsync(id, cancellationToken));

    [HttpPut("sprints/{id:guid}/retrospective")]
    [ProducesResponseType<SprintReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SaveRetrospective(Guid id, SaveRetrospectiveRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await sprints.SaveRetrospectiveAsync(id, request, cancellationToken));

    [HttpGet("filters")]
    [ProducesResponseType<IReadOnlyList<SavedIssueFilterResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SavedIssueFilterResponse>>> Filters(CancellationToken cancellationToken) =>
        Ok(await filters.ListAsync(cancellationToken));

    [HttpPost("filters")]
    [ProducesResponseType<SavedIssueFilterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SaveFilter(SaveIssueFilterRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await filters.CreateAsync(request, cancellationToken));

    [HttpDelete("filters/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteFilter(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await filters.DeleteAsync(id, cancellationToken));
}
