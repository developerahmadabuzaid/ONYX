using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Intelligence;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>Internal competitor references for research only. Nothing here is scraped or shown on the public site.</summary>
[Route("admin/competitors")]
public sealed class CompetitorsController(IAdminCmsApiClient cms) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await cms.ListCompetitorsAsync(cancellationToken);
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(string name, string domain, string? markets, string? languages, string? topics, string? notes, CancellationToken cancellationToken)
    {
        var request = new SaveCompetitorRequest(name ?? string.Empty, domain ?? string.Empty, SplitList(markets), SplitList(languages), SplitList(topics), Blank(notes), true, null);
        var result = await cms.CreateCompetitorAsync(request, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await cms.DeleteCompetitorAsync(id, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Deleted");
    }
}
