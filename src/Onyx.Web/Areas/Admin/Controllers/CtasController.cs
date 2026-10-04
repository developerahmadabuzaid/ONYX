using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Models;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>Reusable calls to action and their defaults (item → content type → global). WhatsApp is not offered at launch.</summary>
[Route("admin/ctas")]
public sealed class CtasController(IAdminCmsApiClient cms) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var ctas = await cms.ListCtasAsync(cancellationToken);
        var defaults = await cms.GetCtaDefaultsAsync(cancellationToken);
        return Failed(ctas) ?? Failed(defaults) ?? View(new CtaListViewModel(ctas.Value!, defaults.Value!));
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(CtaForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var result = await cms.CreateCtaAsync(ToRequest(form, null), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CtaForm form, uint version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var result = await cms.UpdateCtaAsync(id, ToRequest(form, version), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)));
    }

    [HttpPost("defaults")]
    public async Task<IActionResult> SetDefault(string? contentType, Guid? callToActionId, CancellationToken cancellationToken)
    {
        var result = await cms.SetCtaDefaultAsync(new SetCtaDefaultRequest(Blank(contentType), callToActionId), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)));
    }

    private static SaveCtaRequest ToRequest(CtaForm form, uint? version)
    {
        var texts = new List<LocalizedTextDto>();
        if (Blank(form.LabelAr) is not null)
        {
            texts.Add(new LocalizedTextDto("ar", form.LabelAr!.Trim(), Blank(form.SecondaryAr)));
        }

        if (Blank(form.LabelEn) is not null)
        {
            texts.Add(new LocalizedTextDto("en", form.LabelEn!.Trim(), Blank(form.SecondaryEn)));
        }

        return new SaveCtaRequest(form.Key?.Trim() ?? string.Empty, form.ActionType ?? string.Empty, Blank(form.TargetUrl), texts, form.IsActive, version);
    }
}
