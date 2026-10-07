using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Analytics;
using Onyx.Contracts.Analytics;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// Web analytics and the first-party funnel (<c>Analytics.View</c>). Reads local aggregates only (never a live provider call);
/// every report states its period and each source's state. Never cached.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/analytics")]
[HasPermission(Permissions.Analytics.View)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAnalyticsController(AnalyticsReportService reports) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<AnalyticsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Overview([FromQuery] AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await reports.OverviewAsync(query, cancellationToken));

    [HttpGet("traffic")]
    [ProducesResponseType<AnalyticsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Traffic([FromQuery] AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await reports.TrafficAsync(query, cancellationToken));

    [HttpGet("landing-pages")]
    [ProducesResponseType<AnalyticsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> LandingPages([FromQuery] AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await reports.LandingPagesAsync(query, cancellationToken));

    [HttpGet("campaigns")]
    [ProducesResponseType<AnalyticsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Campaigns([FromQuery] AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await reports.CampaignsAsync(query, cancellationToken));

    [HttpGet("conversions")]
    [ProducesResponseType<AnalyticsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Conversions([FromQuery] AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await reports.ConversionsAsync(query, cancellationToken));

    [HttpGet("content")]
    [ProducesResponseType<AnalyticsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Content([FromQuery] AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await reports.ContentAsync(query, cancellationToken));

    /// <summary>The first-party lead funnel (Stage 8 attribution, current lead stages), grouped by one attribution dimension.</summary>
    [HttpGet("funnel")]
    [ProducesResponseType<AnalyticsReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Funnel([FromQuery] AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await reports.FunnelAsync(query, cancellationToken));
}
