using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.News;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// The news source allowlist: add a source (it starts blocked), record the reviewed licensing/terms note, allow or block it,
/// and see its fetch state. Fetching happens only in the Worker; nothing here contacts a feed.
/// </summary>
[Route("admin/news/sources")]
public sealed class NewsSourcesController(IAdminNewsApiClient api) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await api.ListSourcesAsync(cancellationToken);
        if (Failed(result) is { } failed)
        {
            return failed;
        }

        await LoadCategoriesAsync(cancellationToken);
        return View(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await api.GetSourceAsync(id, cancellationToken);
        if (Failed(result) is { } failed)
        {
            return failed;
        }

        await LoadCategoriesAsync(cancellationToken);
        return View(result.Value);
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(SourceForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var result = await api.CreateSourceAsync(form.ToRequest(null), cancellationToken);
        return result.IsSuccess
            ? After(result, RedirectToAction(nameof(Details), new { id = result.Value!.Id }), "Cms.Created")
            : After(result, RedirectToAction(nameof(Index)));
    }

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SourceForm form, uint version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        return After(await api.UpdateSourceAsync(id, form.ToRequest(version), cancellationToken), RedirectToAction(nameof(Details), new { id }));
    }

    [HttpPost("{id:guid}/allow")]
    public async Task<IActionResult> Allow(Guid id, uint version, CancellationToken cancellationToken) =>
        After(await api.ChangeSourceAsync(id, "allow", new NewsVersionRequest(version), cancellationToken), RedirectToAction(nameof(Details), new { id }));

    [HttpPost("{id:guid}/block")]
    public async Task<IActionResult> Block(Guid id, uint version, CancellationToken cancellationToken) =>
        After(await api.ChangeSourceAsync(id, "block", new NewsVersionRequest(version), cancellationToken), RedirectToAction(nameof(Details), new { id }));

    /// <summary>Category names for the form; a source manager without News.View simply sees no names (the API decides).</summary>
    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await api.ListCategoriesAsync(cancellationToken);
        ViewData["Categories"] = categories.IsSuccess ? categories.Value : null;
    }

    public sealed class SourceForm
    {
        public string? Name { get; set; }

        public string? HomepageUrl { get; set; }

        public string? FeedUrl { get; set; }

        public string? FeedFormat { get; set; }

        public string? LanguageCode { get; set; }

        public Guid DefaultCategoryId { get; set; }

        public int FetchIntervalMinutes { get; set; }

        public string? LicensingNote { get; set; }

        public bool AutoPublish { get; set; }

        public SaveNewsSourceRequest ToRequest(uint? version) =>
            new(Name ?? string.Empty, HomepageUrl ?? string.Empty, FeedUrl ?? string.Empty, FeedFormat ?? string.Empty, LanguageCode ?? string.Empty,
                DefaultCategoryId, FetchIntervalMinutes, Blank(LicensingNote), AutoPublish, version);
    }
}
