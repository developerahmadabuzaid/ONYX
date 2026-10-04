using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.Application.Intelligence;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Intelligence;

namespace Onyx.API.Controllers.Admin;

/// <summary>Content intelligence: which data sources are really connected, and rule-based internal-link analysis.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/intelligence")]
public sealed class AdminIntelligenceController(IIntelligenceStatusService status, IInternalLinkAnalyzer links) : ControllerBase
{
    [HttpGet("capabilities")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<IntelligenceCapabilitiesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IntelligenceCapabilitiesResponse>> Capabilities(CancellationToken cancellationToken) =>
        Ok(await status.GetCapabilitiesAsync(cancellationToken));

    [HttpGet("link-report")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<InternalLinkReportResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InternalLinkReportResponse>> LinkReport(CancellationToken cancellationToken) =>
        Ok(await links.AnalyzeAsync(cancellationToken));

    [HttpPost("link-report/recommendations")]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType<GeneratedRecommendationsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GeneratedRecommendationsResponse>> GenerateRecommendations(CancellationToken cancellationToken) =>
        Ok(await links.GenerateRecommendationsAsync(cancellationToken));
}
