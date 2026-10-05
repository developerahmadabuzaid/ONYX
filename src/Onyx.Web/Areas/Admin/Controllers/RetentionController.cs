using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Privacy;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Retention policies. Periods come from the approved privacy policy: nothing is preset, and a category is processed only once
/// it is saved enabled with a period. The preview shows how many records the settings would process now, changing nothing.
/// </summary>
[Route("admin/privacy/retention")]
public sealed class RetentionController(IAdminPrivacyApiClient api) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? previewCategory, string? previewAction, int? previewDays, CancellationToken cancellationToken)
    {
        var result = await api.ListPoliciesAsync(cancellationToken);
        if (Failed(result) is { } failed)
        {
            return failed;
        }

        if (Blank(previewCategory) is { } category && Blank(previewAction) is { } action && previewDays is { } days)
        {
            var preview = await api.PreviewPolicyAsync(category, action, days, cancellationToken);
            if (preview.IsSuccess)
            {
                ViewData["Preview"] = preview.Value;
            }
            else
            {
                AddProblem(preview);
            }
        }

        return View(result.Value);
    }

    [HttpPost("{category}")]
    public async Task<IActionResult> Save(string category, string action, int? periodDays, bool isEnabled, uint? version, CancellationToken cancellationToken) =>
        After(await api.SavePolicyAsync(category, new SaveRetentionPolicyRequest(action ?? string.Empty, periodDays, isEnabled, version), cancellationToken), RedirectToAction(nameof(Index)));
}
