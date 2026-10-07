using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.News;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// News items: review held items, write Onyx's headline and summary per language, publish, hide, unpublish, delete, recategorize
/// and mark indexable. Every POST carries an antiforgery token; the API authorizes and audits each action.
/// </summary>
[Route("admin/news")]
public sealed class NewsItemsController(IAdminNewsApiClient api) : CmsControllerBase
{
    private static readonly HashSet<string> StateActions = new(StringComparer.Ordinal) { "publish", "hide", "unpublish", "delete" };

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, Guid? sourceId, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await api.ListItemsAsync(Blank(status), sourceId, Math.Max(page, 1), cancellationToken);
        if (Failed(result) is { } failed)
        {
            return failed;
        }

        ViewData["Status"] = Blank(status);
        return View(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await api.GetItemAsync(id, cancellationToken);
        if (Failed(result) is { } failed)
        {
            return failed;
        }

        var categories = await api.ListCategoriesAsync(cancellationToken);
        ViewData["Categories"] = categories.IsSuccess ? categories.Value : null;
        return View(result.Value);
    }

    [HttpPost("{id:guid}/translations/{language}")]
    public async Task<IActionResult> EditTranslation(Guid id, string language, string title, string summary, uint version, CancellationToken cancellationToken) =>
        After(
            await api.EditTranslationAsync(id, language, new UpdateNewsTranslationRequest(title ?? string.Empty, summary ?? string.Empty, version), cancellationToken),
            RedirectToAction(nameof(Details), new { id }));

    [HttpPost("{id:guid}/category")]
    public async Task<IActionResult> ChangeCategory(Guid id, Guid categoryId, uint version, CancellationToken cancellationToken) =>
        After(await api.ChangeCategoryAsync(id, new ChangeNewsCategoryRequest(categoryId, version), cancellationToken), RedirectToAction(nameof(Details), new { id }));

    [HttpPost("{id:guid}/state/{operation}")]
    public async Task<IActionResult> ChangeState(Guid id, string operation, uint version, CancellationToken cancellationToken)
    {
        if (!StateActions.Contains(operation))
        {
            return NotFound();
        }

        return After(await api.ChangeStateAsync(id, operation, new NewsVersionRequest(version), cancellationToken), RedirectToAction(nameof(Details), new { id }));
    }

    [HttpPost("{id:guid}/indexing")]
    public async Task<IActionResult> SetIndexing(Guid id, bool indexable, uint version, CancellationToken cancellationToken) =>
        After(await api.SetIndexingAsync(id, new SetNewsIndexingRequest(indexable, version), cancellationToken), RedirectToAction(nameof(Details), new { id }));
}
