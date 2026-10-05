using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Common;
using Onyx.Contracts.Insights;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>Real people who may be credited on insights, with their recorded consent. No invented personas.</summary>
[Route("admin/authors")]
public sealed class AuthorsController(IAdminCmsApiClient cms) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await cms.ListAuthorsAsync(cancellationToken);
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(string? nameAr, string? titleAr, string? nameEn, string? titleEn, string consentNote, CancellationToken cancellationToken)
    {
        var result = await cms.CreateAuthorAsync(new SaveAuthorRequest(Names(nameAr, titleAr, nameEn, titleEn), consentNote ?? string.Empty, true, null), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, string? nameAr, string? titleAr, string? nameEn, string? titleEn, string consentNote, bool isActive, uint version, CancellationToken cancellationToken)
    {
        var result = await cms.UpdateAuthorAsync(id, new SaveAuthorRequest(Names(nameAr, titleAr, nameEn, titleEn), consentNote ?? string.Empty, isActive, version), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)));
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await cms.DeleteAuthorAsync(id, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Deleted");
    }

    private static List<LocalizedTextDto> Names(string? nameAr, string? titleAr, string? nameEn, string? titleEn)
    {
        var names = new List<LocalizedTextDto>();
        if (Blank(nameAr) is { } arabic)
        {
            names.Add(new LocalizedTextDto("ar", arabic, Blank(titleAr)));
        }

        if (Blank(nameEn) is { } english)
        {
            names.Add(new LocalizedTextDto("en", english, Blank(titleEn)));
        }

        return names;
    }
}
