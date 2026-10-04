using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Content;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>Manual redirect rules (internal targets only; the API rejects loops, chains, external targets and live paths).</summary>
[Route("admin/redirects")]
public sealed class RedirectsController(IAdminCmsApiClient cms) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await cms.ListRedirectsAsync(Blank(search), Math.Max(page, 1), cancellationToken);
        ViewData["Search"] = search;
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(string languageCode, string fromPath, string kind, Guid? targetContentItemId, string? targetPath, string? note, CancellationToken cancellationToken)
    {
        var result = await cms.CreateRedirectAsync(new SaveRedirectRequest(languageCode, fromPath ?? string.Empty, kind, targetContentItemId, Blank(targetPath), Blank(note), true), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpPost("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, string languageCode, string fromPath, string kind, Guid? targetContentItemId, string? targetPath, string? note, bool isActive, CancellationToken cancellationToken)
    {
        var result = await cms.UpdateRedirectAsync(id, new SaveRedirectRequest(languageCode, fromPath, kind, targetContentItemId, Blank(targetPath), Blank(note), isActive), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)));
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await cms.DeleteRedirectAsync(id, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Deleted");
    }
}
