using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Common;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Models;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>Media library. Uploads are streamed to the API, which validates type (magic bytes), size and dimensions and strips metadata.</summary>
[Route("admin/media")]
public sealed class MediaLibraryController(IAdminCmsApiClient cms) : CmsControllerBase
{
    /// <summary>Slightly above the API's 10 MB file limit so the API, not Web, reports the precise error.</summary>
    private const long MaxRequestBytes = 11 * 1024 * 1024;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await cms.ListMediaAsync(Blank(search), Math.Max(page, 1), cancellationToken);
        ViewData["Search"] = search;
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ShowError("Cms.Media.NoFile", RedirectToAction(nameof(Index)));
        }

        await using var stream = file.OpenReadStream();
        var result = await cms.UploadMediaAsync(stream, Path.GetFileName(file.FileName), file.ContentType, cancellationToken);
        return After(result, result.IsSuccess ? RedirectToAction(nameof(Details), new { id = result.Value!.Id }) : RedirectToAction(nameof(Index)), "Cms.Media.Uploaded");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var asset = await cms.GetMediaAsync(id, cancellationToken);
        if (Failed(asset) is { } failure)
        {
            return failure;
        }

        var usages = await cms.ListMediaUsagesAsync(id, cancellationToken);
        return Failed(usages) ?? View(new MediaDetailsViewModel(asset.Value!, usages.Value!));
    }

    [HttpPost("{id:guid}/texts")]
    public async Task<IActionResult> Texts(Guid id, string? title, string? altAr, string? captionAr, string? altEn, string? captionEn, uint version, CancellationToken cancellationToken)
    {
        var texts = new List<LocalizedTextDto>();
        if (Blank(altAr) is not null || Blank(captionAr) is not null)
        {
            texts.Add(new LocalizedTextDto("ar", altAr?.Trim() ?? string.Empty, Blank(captionAr)));
        }

        if (Blank(altEn) is not null || Blank(captionEn) is not null)
        {
            texts.Add(new LocalizedTextDto("en", altEn?.Trim() ?? string.Empty, Blank(captionEn)));
        }

        var result = await cms.UpdateMediaTextsAsync(id, new Contracts.Media.UpdateMediaTextsRequest(Blank(title), texts, version), cancellationToken);
        return After(result, RedirectToAction(nameof(Details), new { id }));
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await cms.DeleteMediaAsync(id, cancellationToken);
        return After(result, result.IsSuccess ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Details), new { id }), "Cms.Deleted");
    }

    /// <summary>Admin view of any uploaded file (used or not), streamed through Web; never cached.</summary>
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> Raw(Guid id, CancellationToken cancellationToken)
    {
        var file = await cms.OpenMediaFileAsync(id, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        Response.RegisterForDisposeAsync(file);
        var type = file.ContentType.Split(';')[0].Trim();
        if (type is not ("image/jpeg" or "image/png" or "image/webp" or "image/gif"))
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return File(file.Content, type);
    }
}
