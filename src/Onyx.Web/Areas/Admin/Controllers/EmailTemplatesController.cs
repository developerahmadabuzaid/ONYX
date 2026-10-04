using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Messaging;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Editable email copy (subject, heading, paragraphs, button label, footer) per language. The layout, links and tokens stay
/// code-owned; the API checks placeholders and keeps required ones (e.g. expiry) in place.
/// </summary>
[Route("admin/email-templates")]
public sealed class EmailTemplatesController(IAdminCmsApiClient cms) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await cms.ListEmailTemplatesAsync(cancellationToken);
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("{key}/{languageCode:length(2)}")]
    public async Task<IActionResult> Save(string key, string languageCode, string subject, string heading, string? paragraphs, string? actionLabel, string? footer, uint? version, CancellationToken cancellationToken)
    {
        var paragraphList = (paragraphs ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var request = new SaveEmailTemplateCopyRequest(subject ?? string.Empty, heading ?? string.Empty, paragraphList, Blank(actionLabel), Blank(footer), version);
        var result = await cms.SaveEmailTemplateAsync(key, languageCode, request, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)));
    }

    [HttpPost("{key}/{languageCode:length(2)}/reset")]
    public async Task<IActionResult> Reset(string key, string languageCode, CancellationToken cancellationToken)
    {
        var result = await cms.ResetEmailTemplateAsync(key, languageCode, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Email.Reset");
    }
}
