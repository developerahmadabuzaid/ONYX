using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Analytics;
using Onyx.Contracts.Analytics;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers.Admin;

/// <summary>Organic search performance and deterministic signals (<c>SearchPerformance.View</c>). Local aggregates only; never cached.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/search-performance")]
[HasPermission(Permissions.SearchPerformance.View)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminSearchPerformanceController(AnalyticsReportService reports) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<AnalyticsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Overview([FromQuery] AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await reports.SearchOverviewAsync(query, cancellationToken));

    [HttpGet("queries")]
    [ProducesResponseType<AnalyticsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Queries([FromQuery] AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await reports.SearchQueriesAsync(query, cancellationToken));

    [HttpGet("pages")]
    [ProducesResponseType<AnalyticsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Pages([FromQuery] AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await reports.SearchPagesAsync(query, cancellationToken));

    /// <summary>Observations to review (never applied automatically): low CTR, click trends, query opportunities, visibility.</summary>
    [HttpGet("signals")]
    [ProducesResponseType<AnalyticsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Signals([FromQuery] AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await reports.SearchSignalsAsync(query, cancellationToken));
}
