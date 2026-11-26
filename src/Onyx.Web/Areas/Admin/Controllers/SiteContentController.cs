using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Onyx.Contracts.Media;
using Onyx.Contracts.SiteContent;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.SiteContent;
using Onyx.Web.Content.SiteContent;
using Onyx.Web.Content.SitePages;
using Onyx.Web.Controllers;
using Onyx.Web.Localization;
using Onyx.Web.Resources;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Editing the code-owned public site: site pages, the homepage and navigation &amp; footer. Editors save drafts, preview them
/// on the real public templates, publish (the public site picks the change up at once on this instance) and restore earlier
/// versions. The API authorizes every call (Pages.View / Pages.Edit / Pages.Publish; the settings document needs Settings.Manage).
/// </summary>
[Route("admin/site-content")]
public sealed class SiteContentController(
    IAdminSiteContentApiClient siteContent,
    IAdminCmsApiClient cms,
    SitePageCatalog catalog,
    SiteContentProvider provider,
    SiteContentContext context,
    LanguageCatalog languages,
    SeoContext seo,
    IStringLocalizer<SharedResource> localizer) : CmsControllerBase
{
    private IReadOnlyList<string> LanguageCodes => languages.Languages.Select(language => language.Code).ToList();

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var documents = await siteContent.ListAsync(cancellationToken);
        if (Failed(documents) is { } failed)
        {
            return failed;
        }

        return View(new SiteContentIndexModel(
            catalog.Pages,
            documents.Value!.ToDictionary(document => document.Key, StringComparer.Ordinal),
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));
    }

    // ------------------------------------------------------------------------------------------------ Site pages ---

    [HttpGet("pages/{page}")]
    public async Task<IActionResult> Page(string page, CancellationToken cancellationToken)
    {
        if (catalog.Find(page) is not { } definition)
        {
            return NotFound();
        }

        var key = SiteContentKeys.Page(page);
        var document = await siteContent.GetAsync(key, cancellationToken);
        if (Failed(document) is { } failed)
        {
            return failed;
        }

        var current = SiteContentSnapshot.TryParse<PageOverride>(document.Value!.DraftJson ?? document.Value.PublishedJson) ?? new PageOverride();
        return View(new PageEditorModel(
            definition,
            current,
            document.Value,
            await ImagesAsync(cancellationToken),
            await RevisionsAsync(key, cancellationToken),
            LanguageCodes,
            LanguageCodes.ToDictionary(code => code, code => catalog.PathFor(page, code) ?? $"/{code}/", StringComparer.Ordinal)));
    }

    [HttpPost("pages/{page}")]
    public async Task<IActionResult> SavePage(string page, uint version, bool exists, CancellationToken cancellationToken)
    {
        if (catalog.Find(page) is not { } definition)
        {
            return NotFound();
        }

        var form = Request.Form.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(SiteContentForms.BuildPage(definition, form), SiteContentSnapshot.Json);
        var result = await siteContent.SaveDraftAsync(SiteContentKeys.Page(page), new SaveSiteContentDraftRequest(json, exists ? version : null), cancellationToken);
        return After(result, RedirectToAction(nameof(Page), new { page }), "SiteContent.DraftSaved");
    }

    [HttpGet("pages/{page}/preview")]
    public async Task<IActionResult> PreviewPage(string page, string? lang, CancellationToken cancellationToken)
    {
        if (catalog.Find(page) is null)
        {
            return NotFound();
        }

        var key = SiteContentKeys.Page(page);
        var document = await siteContent.GetAsync(key, cancellationToken);
        if (Failed(document) is { } failed)
        {
            return failed;
        }

        var language = UsePreviewLanguage(lang);
        await provider.EnsureFreshAsync(cancellationToken);
        context.UsePreview(key, document.Value!.DraftJson ?? document.Value.PublishedJson);
        var controller = ActivatorUtilities.CreateInstance<SitePageController>(HttpContext.RequestServices);
        controller.ControllerContext = ControllerContext;
        var view = controller.Show(language, page);
        seo.NoIndex = seo.NoFollow = true;
        return view;
    }

    // ---------------------------------------------------------------------------------- Homepage & navigation ---

    [HttpGet("home")]
    public Task<IActionResult> Home(CancellationToken cancellationToken) => CopyEditorAsync(SiteContentKeys.Home, cancellationToken);

    [HttpPost("home")]
    public Task<IActionResult> SaveHome(uint version, bool exists, CancellationToken cancellationToken) =>
        SaveCopyAsync(SiteContentKeys.Home, version, exists, nameof(Home), cancellationToken);

    [HttpGet("home/preview")]
    public async Task<IActionResult> PreviewHome(string? lang, CancellationToken cancellationToken)
    {
        var document = await siteContent.GetAsync(SiteContentKeys.Home, cancellationToken);
        if (Failed(document) is { } failed)
        {
            return failed;
        }

        var language = UsePreviewLanguage(lang);
        await provider.EnsureFreshAsync(cancellationToken);
        context.UsePreview(SiteContentKeys.Home, document.Value!.DraftJson ?? document.Value.PublishedJson);
        var controller = ActivatorUtilities.CreateInstance<HomeController>(HttpContext.RequestServices);
        controller.ControllerContext = ControllerContext;
        var view = await controller.Index(language, cancellationToken);
        seo.NoIndex = seo.NoFollow = true;
        return view;
    }

    [HttpGet("shared")]
    public Task<IActionResult> Shared(CancellationToken cancellationToken) => CopyEditorAsync(SiteContentKeys.Shared, cancellationToken);

    [HttpPost("shared")]
    public Task<IActionResult> SaveShared(uint version, bool exists, CancellationToken cancellationToken) =>
        SaveCopyAsync(SiteContentKeys.Shared, version, exists, nameof(Shared), cancellationToken);

    // ----------------------------------------------------------------------------- Publish, discard, restore ---

    [HttpPost("publish")]
    public async Task<IActionResult> Publish(string key, uint version, string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await siteContent.PublishAsync(key, version, cancellationToken);
        if (result.IsSuccess)
        {
            await provider.RefreshNowAsync(cancellationToken);
        }

        return After(result, Back(returnUrl), "SiteContent.Published");
    }

    [HttpPost("discard")]
    public async Task<IActionResult> Discard(string key, uint version, string? returnUrl, CancellationToken cancellationToken) =>
        After(await siteContent.DiscardAsync(key, version, cancellationToken), Back(returnUrl), "SiteContent.Discarded");

    [HttpPost("restore")]
    public async Task<IActionResult> Restore(string key, int number, uint version, string? returnUrl, CancellationToken cancellationToken) =>
        After(await siteContent.RestoreAsync(key, number, version, cancellationToken), Back(returnUrl), "SiteContent.Restored");

    // ------------------------------------------------------------------------------------------------ Helpers ---

    private async Task<IActionResult> CopyEditorAsync(string key, CancellationToken cancellationToken)
    {
        var document = await siteContent.GetAsync(key, cancellationToken);
        if (Failed(document) is { } failed)
        {
            return failed;
        }

        var groups = key == SiteContentKeys.Home ? SiteCopyCatalog.Home : SiteCopyCatalog.Shared;
        var current = SiteContentSnapshot.TryParse<CopyOverride>(document.Value!.DraftJson ?? document.Value.PublishedJson) ?? new CopyOverride();
        return View("Copy", new CopyEditorModel(
            key,
            groups,
            Defaults(groups),
            current,
            document.Value,
            key == SiteContentKeys.Home ? await ImagesAsync(cancellationToken) : [],
            await RevisionsAsync(key, cancellationToken),
            LanguageCodes));
    }

    private async Task<IActionResult> SaveCopyAsync(string key, uint version, bool exists, string editor, CancellationToken cancellationToken)
    {
        var groups = key == SiteContentKeys.Home ? SiteCopyCatalog.Home : SiteCopyCatalog.Shared;
        var form = Request.Form.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(SiteContentForms.BuildCopy(groups, Defaults(groups), form, key == SiteContentKeys.Home), SiteContentSnapshot.Json);
        var result = await siteContent.SaveDraftAsync(key, new SaveSiteContentDraftRequest(json, exists ? version : null), cancellationToken);
        return After(result, RedirectToAction(editor), "SiteContent.DraftSaved");
    }

    /// <summary>Resource-file text of every editable key in every language (the fallback the editors compare against).</summary>
    private Dictionary<string, IReadOnlyDictionary<string, string>> Defaults(IReadOnlyList<CopyGroup> groups)
    {
        var defaults = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        var original = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var code in LanguageCodes)
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(code);
                defaults[code] = groups.SelectMany(group => group.Keys).Distinct(StringComparer.Ordinal)
                    .ToDictionary(key => key, key => localizer[key].Value, StringComparer.Ordinal);
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }

        return defaults;
    }

    private async Task<IReadOnlyList<MediaAssetResponse>> ImagesAsync(CancellationToken cancellationToken)
    {
        // Only the media list is optional here: without Media.View the pickers are simply empty.
        var images = await cms.ListMediaAsync(null, 1, cancellationToken);
        return images.IsSuccess && images.Value is not null ? images.Value.Items : [];
    }

    private async Task<IReadOnlyList<SiteContentRevisionResponse>> RevisionsAsync(string key, CancellationToken cancellationToken)
    {
        var revisions = await siteContent.ListRevisionsAsync(key, cancellationToken);
        return revisions.IsSuccess && revisions.Value is not null ? revisions.Value : [];
    }

    private string UsePreviewLanguage(string? lang)
    {
        var language = languages.IsSupported(lang) ? lang! : languages.DefaultLanguage;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        return language;
    }

    private IActionResult Back(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith("/admin/", StringComparison.Ordinal)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
}
