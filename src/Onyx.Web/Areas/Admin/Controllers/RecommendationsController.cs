using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Intelligence;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// SEO recommendations: proposed → approved/rejected (Seo.Approve) → applied (Seo.Edit, as a new draft revision, never auto-published).
/// Manual proposals carry no provider metrics.
/// </summary>
[Route("admin/seo-recommendations")]
public sealed class RecommendationsController(IAdminCmsApiClient cms) : CmsControllerBase
{
    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal) { "approve", "reject", "apply", "mark-applied" };

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await cms.ListRecommendationsAsync(Blank(status), Math.Max(page, 1), cancellationToken);
        ViewData["Status"] = status;
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("")]
    public async Task<IActionResult> Propose(
        string kind, string title, string detectedIssue, string? reasoning, Guid? contentItemId, string? languageCode, Guid? clusterId,
        string? field, string? currentValue, string? proposedValue, CancellationToken cancellationToken)
    {
        var changes = Blank(field) is { } fieldName ? new[] { new ProposedChangeDto(fieldName, Blank(currentValue), Blank(proposedValue)) } : [];
        var request = new ProposeRecommendationRequest(kind, title ?? string.Empty, detectedIssue ?? string.Empty, Blank(reasoning), changes, contentItemId, Blank(languageCode), clusterId);
        var result = await cms.ProposeRecommendationAsync(request, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpPost("{id:guid}/decide")]
    public async Task<IActionResult> Decide(Guid id, string operation, string? note, uint version, CancellationToken cancellationToken)
    {
        if (!Actions.Contains(operation))
        {
            return ShowError("Cms.InvalidAction", RedirectToAction(nameof(Index)));
        }

        var result = await cms.RecommendationActionAsync(id, operation, new DecideRequest(Blank(note), version), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Done");
    }
}
