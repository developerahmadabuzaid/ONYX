using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Onyx.Contracts.Content;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Models;
using Onyx.Web.Resources;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// CMS content: services, pillars, industries and pages. Editing, review, publishing, revisions, previews and relations each map to
/// one API call; the API enforces the lifecycle and every permission.
/// </summary>
[Route("admin/content")]
public sealed class ContentController(IAdminCmsApiClient cms, IStringLocalizer<SharedResource> localizer) : CmsControllerBase
{
    private static readonly HashSet<string> VersionOnlyActions = new(StringComparer.Ordinal)
    {
        "submit", "approve", "publish", "schedule/cancel", "unpublish-schedule/cancel", "restore",
    };

    [HttpGet("")]
    public async Task<IActionResult> Index(string? type, string? status, bool needsUrlDecision = false, string? search = null, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await cms.ListContentAsync(Blank(type), Blank(status), needsUrlDecision, Blank(search), Math.Max(page, 1), cancellationToken);
        return Failed(result) ?? View(new ContentListViewModel(result.Value!, type, status, needsUrlDecision, search));
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(string type, Guid? parentId, string? key, string? presentation, int sortOrder, CancellationToken cancellationToken)
    {
        var result = await cms.CreateContentAsync(new CreateContentRequest(type, parentId, Blank(key), Blank(presentation), sortOrder), cancellationToken);
        return After(result, result.IsSuccess ? RedirectToAction(nameof(Overview), new { id = result.Value!.Id }) : RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Overview(Guid id, CancellationToken cancellationToken) => await OverviewAsync(id, null, cancellationToken);

    [HttpPost("{id:guid}/settings")]
    public async Task<IActionResult> Settings(Guid id, Guid? parentId, string? key, string presentation, int sortOrder, Guid? primaryCtaId, Guid? featuredImageId, uint version, CancellationToken cancellationToken)
    {
        var result = await cms.UpdateSettingsAsync(id, new UpdateContentSettingsRequest(parentId, Blank(key), presentation, sortOrder, primaryCtaId, featuredImageId, version), cancellationToken);
        return After(result, RedirectToAction(nameof(Overview), new { id }));
    }

    [HttpGet("{id:guid}/translations/{languageCode:length(2)}")]
    public async Task<IActionResult> Translation(Guid id, string languageCode, CancellationToken cancellationToken)
    {
        var result = await cms.GetContentAsync(id, cancellationToken);
        if (Failed(result) is { } failure)
        {
            return failure;
        }

        var content = result.Value!;
        var translation = content.Translations.FirstOrDefault(item => item.LanguageCode == languageCode);
        var form = new ContentTranslationForm
        {
            Title = translation?.Title ?? string.Empty,
            Summary = translation?.Summary,
            Slug = translation?.Slug ?? string.Empty,
            BodyText = BlockTextFormat.Format(translation?.Body),
            FaqText = BlockTextFormat.FormatFaqs(translation?.Faqs),
            Version = content.Version,
        };
        return View(new ContentEditorViewModel(content, languageCode, form, null));
    }

    [HttpPost("{id:guid}/translations/{languageCode:length(2)}")]
    public async Task<IActionResult> Translation(Guid id, string languageCode, ContentTranslationForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var body = BlockTextFormat.Parse(form.BodyText);
        var faqs = BlockTextFormat.ParseFaqs(form.FaqText);
        foreach (var error in body.Errors.Concat(faqs.Errors))
        {
            ModelState.AddModelError(string.Empty, localizer[error.ResourceKey, error.Line].Value);
        }

        if (ModelState.ErrorCount == 0)
        {
            var request = new UpdateContentTranslationRequest(form.Title.Trim(), Blank(form.Summary), form.Slug.Trim(), body.Blocks, faqs.Faqs, form.Version);
            var result = await cms.UpdateTranslationAsync(id, languageCode, request, cancellationToken);
            if (result.IsSuccess)
            {
                TempData[NoticeKey] = "Cms.Saved";
                return RedirectToAction(nameof(Translation), new { id, languageCode });
            }

            if (result.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return Forbid();
            }

            AddProblem(result);
        }

        var current = await cms.GetContentAsync(id, cancellationToken);
        return Failed(current) ?? View(new ContentEditorViewModel(current.Value!, languageCode, form, null));
    }

    [HttpGet("{id:guid}/seo/{languageCode:length(2)}")]
    public async Task<IActionResult> Seo(Guid id, string languageCode, CancellationToken cancellationToken)
    {
        var result = await cms.GetContentAsync(id, cancellationToken);
        if (Failed(result) is { } failure)
        {
            return failure;
        }

        var content = result.Value!;
        var seo = content.Translations.FirstOrDefault(item => item.LanguageCode == languageCode)?.Seo;
        var form = new ContentSeoForm
        {
            PrimaryKeyword = seo?.PrimaryKeyword,
            SecondaryKeywords = seo?.SecondaryKeywords is null ? null : string.Join(", ", seo.SecondaryKeywords),
            SearchIntent = seo?.SearchIntent,
            MetaTitle = seo?.MetaTitle,
            MetaDescription = seo?.MetaDescription,
            CanonicalPath = seo?.CanonicalPath,
            Index = seo?.Index ?? true,
            Follow = seo?.Follow ?? true,
            StructuredData = seo?.StructuredData?.ToList() ?? [],
            OpenGraphTitle = seo?.OpenGraphTitle,
            OpenGraphDescription = seo?.OpenGraphDescription,
            OpenGraphImageId = seo?.OpenGraphImageId,
            TargetMarket = seo?.TargetMarket,
            Status = seo?.Status,
            Notes = seo?.Notes,
            Version = content.Version,
        };
        return View(new ContentEditorViewModel(content, languageCode, null, form));
    }

    [HttpPost("{id:guid}/seo/{languageCode:length(2)}")]
    public async Task<IActionResult> Seo(Guid id, string languageCode, ContentSeoForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var profile = new SeoProfileDto(
            Blank(form.PrimaryKeyword), SplitList(form.SecondaryKeywords), Blank(form.SearchIntent), Blank(form.MetaTitle), Blank(form.MetaDescription),
            Blank(form.CanonicalPath), form.Index, form.Follow, form.StructuredData, Blank(form.OpenGraphTitle), Blank(form.OpenGraphDescription),
            form.OpenGraphImageId, Blank(form.TargetMarket), Blank(form.Status), Blank(form.Notes));
        var result = await cms.UpdateSeoAsync(id, languageCode, new UpdateContentSeoRequest(profile, form.Version), cancellationToken);
        if (result.IsSuccess)
        {
            TempData[NoticeKey] = "Cms.Saved";
            return RedirectToAction(nameof(Seo), new { id, languageCode });
        }

        if (result.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return Forbid();
        }

        AddProblem(result);
        var current = await cms.GetContentAsync(id, cancellationToken);
        return Failed(current) ?? View(new ContentEditorViewModel(current.Value!, languageCode, null, form));
    }

    /// <summary>Lifecycle transitions. Each maps to its own API endpoint and permission (submit ≠ approve ≠ publish).</summary>
    [HttpPost("{id:guid}/lifecycle")]
    public async Task<IActionResult> Lifecycle(
        Guid id, string operation, uint version, string? note, DateTime? at, string? timeZone, string? resolution, Guid? redirectTo, CancellationToken cancellationToken)
    {
        object? request = operation switch
        {
            _ when VersionOnlyActions.Contains(operation) => new ContentVersionRequest(version),
            "reject" => new RejectContentRequest(note ?? string.Empty, version),
            "schedule" when at is not null => new ScheduleContentRequest(AsUtc(at.Value), Blank(timeZone), version),
            "unpublish-schedule" when at is not null => new ScheduleUnpublishRequest(AsUtc(at.Value), version),
            "unpublish" or "archive" or "url-resolution" => new TakeOfflineRequest(resolution ?? "PendingDecision", redirectTo, version),
            _ => null,
        };
        if (request is null)
        {
            return ShowError("Cms.InvalidAction", RedirectToAction(nameof(Overview), new { id }));
        }

        var result = await cms.TransitionAsync(id, operation, request, cancellationToken);
        return After(result, RedirectToAction(nameof(Overview), new { id }), "Cms.Done");
    }

    [HttpPost("{id:guid}/preview/{languageCode:length(2)}")]
    public async Task<IActionResult> Preview(Guid id, string languageCode, CancellationToken cancellationToken)
    {
        var link = await cms.CreatePreviewLinkAsync(id, languageCode, cancellationToken);
        if (!link.IsSuccess)
        {
            return After(link, RedirectToAction(nameof(Overview), new { id }));
        }

        return await OverviewAsync(id, link.Value, cancellationToken);
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, string confirmation, uint version, CancellationToken cancellationToken)
    {
        var result = await cms.DeleteContentAsync(id, new DeleteContentRequest(confirmation ?? string.Empty, version), cancellationToken);
        return After(result, result.IsSuccess ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Overview), new { id }), "Cms.Deleted");
    }

    [HttpGet("{id:guid}/revisions")]
    public async Task<IActionResult> Revisions(Guid id, int? from, int? to, CancellationToken cancellationToken)
    {
        var content = await cms.GetContentAsync(id, cancellationToken);
        if (Failed(content) is { } failure)
        {
            return failure;
        }

        var revisions = await cms.ListRevisionsAsync(id, cancellationToken);
        if (Failed(revisions) is { } revisionFailure)
        {
            return revisionFailure;
        }

        RevisionComparisonResponse? comparison = null;
        if (from is { } fromNumber && to is { } toNumber)
        {
            var compared = await cms.CompareRevisionsAsync(id, fromNumber, toNumber, cancellationToken);
            if (compared.IsSuccess)
            {
                comparison = compared.Value;
            }
            else
            {
                AddProblem(compared);
            }
        }

        return View(new ContentRevisionsViewModel(content.Value!, revisions.Value!, comparison));
    }

    [HttpPost("{id:guid}/revisions/{number:int}/restore")]
    public async Task<IActionResult> RestoreRevision(Guid id, int number, uint version, string? note, CancellationToken cancellationToken)
    {
        var result = await cms.RestoreRevisionAsync(id, number, new RestoreRevisionRequest(version, Blank(note)), cancellationToken);
        return After(result, RedirectToAction(nameof(Revisions), new { id }), "Cms.Restored");
    }

    [HttpGet("{id:guid}/relations")]
    public async Task<IActionResult> Relations(Guid id, CancellationToken cancellationToken)
    {
        var content = await cms.GetContentAsync(id, cancellationToken);
        if (Failed(content) is { } failure)
        {
            return failure;
        }

        var relations = await cms.ListRelationsAsync(id, cancellationToken);
        var suggestions = await cms.SuggestRelatedAsync(id, cancellationToken);
        return Failed(relations) ?? Failed(suggestions) ?? View(new ContentRelationsViewModel(content.Value!, relations.Value!, suggestions.Value!));
    }

    [HttpPost("{id:guid}/relations")]
    public async Task<IActionResult> AddRelation(Guid id, Guid toContentItemId, string kind, int position, string? note, CancellationToken cancellationToken)
    {
        var result = await cms.AddRelationAsync(id, new AddContentRelationRequest(toContentItemId, kind, position, Blank(note)), cancellationToken);
        return After(result, RedirectToAction(nameof(Relations), new { id }));
    }

    [HttpPost("{id:guid}/relations/{relationId:guid}/delete")]
    public async Task<IActionResult> RemoveRelation(Guid id, Guid relationId, CancellationToken cancellationToken)
    {
        var result = await cms.RemoveRelationAsync(id, relationId, cancellationToken);
        return After(result, RedirectToAction(nameof(Relations), new { id }), "Cms.Deleted");
    }

    [HttpGet("{id:guid}/intelligence/{languageCode:length(2)}")]
    public async Task<IActionResult> Intelligence(Guid id, string languageCode, CancellationToken cancellationToken)
    {
        var content = await cms.GetContentAsync(id, cancellationToken);
        if (Failed(content) is { } failure)
        {
            return failure;
        }

        var intelligence = await cms.GetIntelligenceAsync(id, languageCode, cancellationToken);
        return Failed(intelligence) ?? View(new ContentIntelligenceViewModel(content.Value!, intelligence.Value!));
    }

    private async Task<IActionResult> OverviewAsync(Guid id, PreviewLinkResponse? previewLink, CancellationToken cancellationToken)
    {
        var content = await cms.GetContentAsync(id, cancellationToken);
        if (Failed(content) is { } failure)
        {
            return failure;
        }

        var quality = await cms.GetQualityAsync(id, cancellationToken);
        var timing = await cms.GetPublishingTimeAsync(id, cancellationToken);
        return View("Overview", new ContentOverviewViewModel(content.Value!, quality.IsSuccess ? quality.Value : null, timing.IsSuccess ? timing.Value : null, previewLink));
    }

    /// <summary>Form date-times are entered in UTC (labelled as such); the API stores UTC.</summary>
    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
