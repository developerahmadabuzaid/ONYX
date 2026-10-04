using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Intelligence;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>Content calendar: proposed topics are accepted or rejected by a person; nothing is drafted or published automatically.</summary>
[Route("admin/content-calendar")]
public sealed class CalendarController(IAdminCmsApiClient cms) : CmsControllerBase
{
    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal) { "accept", "reject", "complete" };

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await cms.ListCalendarAsync(Blank(status), Math.Max(page, 1), cancellationToken);
        ViewData["Status"] = status;
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("")]
    public async Task<IActionResult> Propose(
        string topic, string languageCode, string? targetMarket, Guid? targetServiceId, Guid? clusterId, string? keyword, string? searchIntent, string? reason,
        DateTime? proposedPublishAt, CancellationToken cancellationToken)
    {
        var at = proposedPublishAt is { } value ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)) : (DateTimeOffset?)null;
        var request = new ProposeCalendarEntryRequest(topic ?? string.Empty, languageCode, Blank(targetMarket), targetServiceId, clusterId, Blank(keyword), Blank(searchIntent), Blank(reason), null, at);
        var result = await cms.ProposeCalendarEntryAsync(request, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpPost("{id:guid}/decide")]
    public async Task<IActionResult> Decide(Guid id, string operation, string? note, uint version, CancellationToken cancellationToken)
    {
        if (!Actions.Contains(operation))
        {
            return ShowError("Cms.InvalidAction", RedirectToAction(nameof(Index)));
        }

        var result = await cms.CalendarActionAsync(id, operation, new DecideRequest(Blank(note), version), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Done");
    }

    [HttpPost("{id:guid}/link")]
    public async Task<IActionResult> Link(Guid id, Guid contentItemId, uint version, CancellationToken cancellationToken)
    {
        var result = await cms.LinkCalendarContentAsync(id, new LinkCalendarContentRequest(contentItemId, version), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)));
    }
}
