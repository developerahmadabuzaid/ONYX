using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.SiteContent;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.SiteContent;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// Editor overrides for the code-owned public site: the homepage (<c>home</c>), navigation and footer (<c>shared</c>) and site
/// pages (<c>page:{key}</c>). Website settings have their own endpoint (<see cref="AdminSiteSettingsController"/>).
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/site-content")]
public sealed class AdminSiteContentController(ISiteContentService siteContent) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Pages.View)]
    [ProducesResponseType<IReadOnlyList<SiteContentDocumentSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SiteContentDocumentSummaryResponse>>> List(CancellationToken cancellationToken) =>
        Ok((await siteContent.ListAsync(cancellationToken)).Where(document => IsPageContent(document.Key)).ToList());

    [HttpGet("{key}")]
    [HasPermission(Permissions.Pages.View)]
    [ProducesResponseType<SiteContentDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(string key, CancellationToken cancellationToken) =>
        IsPageContent(key) ? this.OkOrProblem(await siteContent.GetAsync(key, cancellationToken)) : NotFound();

    [HttpPut("{key}/draft")]
    [HasPermission(Permissions.Pages.Edit)]
    [ProducesResponseType<SiteContentDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SaveDraft(string key, SaveSiteContentDraftRequest request, CancellationToken cancellationToken) =>
        IsPageContent(key) ? this.OkOrProblem(await siteContent.SaveDraftAsync(key, request, cancellationToken)) : NotFound();

    [HttpPost("{key}/publish")]
    [HasPermission(Permissions.Pages.Publish)]
    [ProducesResponseType<SiteContentDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Publish(string key, SiteContentVersionRequest request, CancellationToken cancellationToken) =>
        IsPageContent(key) ? this.OkOrProblem(await siteContent.PublishAsync(key, request, cancellationToken)) : NotFound();

    [HttpPost("{key}/discard")]
    [HasPermission(Permissions.Pages.Edit)]
    [ProducesResponseType<SiteContentDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Discard(string key, SiteContentVersionRequest request, CancellationToken cancellationToken) =>
        IsPageContent(key) ? this.OkOrProblem(await siteContent.DiscardDraftAsync(key, request, cancellationToken)) : NotFound();

    [HttpGet("{key}/revisions")]
    [HasPermission(Permissions.Pages.View)]
    [ProducesResponseType<IReadOnlyList<SiteContentRevisionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Revisions(string key, CancellationToken cancellationToken) =>
        IsPageContent(key) ? this.OkOrProblem(await siteContent.ListRevisionsAsync(key, cancellationToken)) : NotFound();

    [HttpPost("{key}/revisions/{number:int}/restore")]
    [HasPermission(Permissions.Pages.Edit)]
    [ProducesResponseType<SiteContentDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Restore(string key, int number, SiteContentVersionRequest request, CancellationToken cancellationToken) =>
        IsPageContent(key) ? this.OkOrProblem(await siteContent.RestoreRevisionAsync(key, number, request, cancellationToken)) : NotFound();

    private static bool IsPageContent(string key) => key != SiteContentKeys.Settings;
}
