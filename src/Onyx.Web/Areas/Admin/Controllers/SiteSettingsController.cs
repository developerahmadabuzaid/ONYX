using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Onyx.Contracts.SiteContent;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.SiteContent;
using Onyx.Web.Content.SiteContent;
using Onyx.Web.Localization;
using Onyx.Web.Resources;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Website settings: company display name, contact email, phones, offices and social links (Settings.Manage). Fields stay empty
/// until real values are entered; publish, discard and restore go through <see cref="SiteContentController"/>.
/// </summary>
[Route("admin/site-settings")]
public sealed class SiteSettingsController(IAdminSiteContentApiClient siteContent, LanguageCatalog languages, IStringLocalizer<SharedResource> localizer) : CmsControllerBase
{
    private static readonly string[] VerifiedOfficeKeys = ["Public.Footer.Office.Riyadh", "Public.Footer.Office.Amman"];

    private IReadOnlyList<string> LanguageCodes => languages.Languages.Select(language => language.Code).ToList();

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var document = await siteContent.GetAsync(SiteContentKeys.Settings, cancellationToken);
        if (Failed(document) is { } failed)
        {
            return failed;
        }

        var revisions = await siteContent.ListRevisionsAsync(SiteContentKeys.Settings, cancellationToken);
        var current = SiteContentSnapshot.TryParse<SiteSettingsContent>(document.Value!.DraftJson ?? document.Value.PublishedJson) ?? new SiteSettingsContent();
        return View(new SettingsEditorModel(
            current,
            document.Value,
            revisions.IsSuccess && revisions.Value is not null ? revisions.Value : [],
            LanguageCodes,
            VerifiedOffices()));
    }

    [HttpPost("")]
    public async Task<IActionResult> Save(uint version, bool exists, CancellationToken cancellationToken)
    {
        var form = Request.Form.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var settings = SiteContentForms.BuildSettings(form, LanguageCodes, SettingsEditorModel.PhoneRows, SettingsEditorModel.OfficeRows, SettingsEditorModel.SocialRows);
        var json = JsonSerializer.Serialize(settings, SiteContentSnapshot.Json);
        var result = await siteContent.SaveDraftAsync(SiteContentKeys.Settings, new SaveSiteContentDraftRequest(json, exists ? version : null), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "SiteContent.DraftSaved");
    }

    /// <summary>The offices the public footer shows when settings have none, in every language (resource-file text).</summary>
    private IReadOnlyList<SiteOffice> VerifiedOffices()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            return VerifiedOfficeKeys.Select(key => new SiteOffice
            {
                Name = LanguageCodes.ToDictionary(code => code, code =>
                {
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(code);
                    return localizer[key].Value;
                }, StringComparer.Ordinal),
            }).ToList();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
