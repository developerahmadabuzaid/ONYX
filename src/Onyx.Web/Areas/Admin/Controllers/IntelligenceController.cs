using Microsoft.AspNetCore.Mvc;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Models;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>Data-source status, governance rules and the internal link report (computed from Onyx's own content, no external data).</summary>
[Route("admin/intelligence")]
public sealed class IntelligenceController(IAdminCmsApiClient cms) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var capabilities = await cms.GetCapabilitiesAsync(cancellationToken);
        var report = await cms.GetLinkReportAsync(cancellationToken);
        return Failed(capabilities) ?? Failed(report) ?? View(new IntelligenceOverviewViewModel(capabilities.Value!, report.Value!));
    }

    [HttpPost("link-recommendations")]
    public async Task<IActionResult> Generate(CancellationToken cancellationToken)
    {
        var result = await cms.GenerateLinkRecommendationsAsync(cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Intelligence.Generated");
    }
}
