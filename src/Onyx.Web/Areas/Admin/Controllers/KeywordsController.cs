using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Intelligence;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Models;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>Keyword and content map. No search volume or difficulty is shown: none comes from a connected provider yet.</summary>
[Route("admin/keywords")]
public sealed class KeywordsController(IAdminCmsApiClient cms) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, int page = 1, string? checkLanguage = null, string? checkKeyword = null, CancellationToken cancellationToken = default)
    {
        var result = await cms.ListKeywordsAsync(Blank(search), Math.Max(page, 1), cancellationToken);
        if (Failed(result) is { } failure)
        {
            return failure;
        }

        KeywordConflictResponse? conflicts = null;
        if (Blank(checkLanguage) is { } language && Blank(checkKeyword) is { } keyword)
        {
            var check = await cms.CheckKeywordAsync(language, keyword, cancellationToken);
            if (check.IsSuccess)
            {
                conflicts = check.Value;
            }
            else
            {
                AddProblem(check);
            }
        }

        return View(new KeywordListViewModel(result.Value!, search, checkKeyword, conflicts));
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(
        string keyword, string languageCode, string? targetMarket, string searchIntent, Guid? targetContentItemId, Guid? clusterId, string status, string? notes, CancellationToken cancellationToken)
    {
        var request = new SaveKeywordTargetRequest(keyword ?? string.Empty, languageCode, Blank(targetMarket), searchIntent, targetContentItemId, clusterId, status, Blank(notes), null);
        var result = await cms.CreateKeywordAsync(request, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await cms.DeleteKeywordAsync(id, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Deleted");
    }
}
