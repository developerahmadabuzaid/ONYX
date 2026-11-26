using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.SiteContent;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.SiteContent;

namespace Onyx.API.Controllers.Admin;

/// <summary>Website settings (verified contact details, offices, social links): a sensitive document, Settings.Manage only.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/site-settings")]
public sealed class AdminSiteSettingsController(ISiteContentService siteContent) : ControllerBase
{
    private const string Key = SiteContentKeys.Settings;

    [HttpGet]
    [HasPermission(Permissions.Settings.Manage)]
    [ProducesResponseType<SiteContentDocumentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(CancellationToken cancellationToken) => this.OkOrProblem(await siteContent.GetAsync(Key, cancellationToken));

    [HttpPut("draft")]
    [HasPermission(Permissions.Settings.Manage)]
    [ProducesResponseType<SiteContentDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SaveDraft(SaveSiteContentDraftRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await siteContent.SaveDraftAsync(Key, request, cancellationToken));

    [HttpPost("publish")]
    [HasPermission(Permissions.Settings.Manage)]
    [ProducesResponseType<SiteContentDocumentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Publish(SiteContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await siteContent.PublishAsync(Key, request, cancellationToken));

    [HttpPost("discard")]
    [HasPermission(Permissions.Settings.Manage)]
    [ProducesResponseType<SiteContentDocumentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Discard(SiteContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await siteContent.DiscardDraftAsync(Key, request, cancellationToken));

    [HttpGet("revisions")]
    [HasPermission(Permissions.Settings.Manage)]
    [ProducesResponseType<IReadOnlyList<SiteContentRevisionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Revisions(CancellationToken cancellationToken) => this.OkOrProblem(await siteContent.ListRevisionsAsync(Key, cancellationToken));

    [HttpPost("revisions/{number:int}/restore")]
    [HasPermission(Permissions.Settings.Manage)]
    [ProducesResponseType<SiteContentDocumentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Restore(int number, SiteContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await siteContent.RestoreRevisionAsync(Key, number, request, cancellationToken));
}
