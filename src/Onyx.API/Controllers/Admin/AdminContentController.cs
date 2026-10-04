using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Content;
using Onyx.Application.Intelligence;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// CMS content: editing, lifecycle, revisions, previews and relations. Each action requires its lifecycle permission; the
/// application layer additionally enforces the content-type scope ({Type}.View / {Type}.Edit).
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/content")]
public sealed class AdminContentController(
    IContentAdministrationService administration,
    IContentWorkflowService workflow,
    IContentRevisionService revisions,
    IContentPreviewService previews,
    IIntelligenceStatusService intelligence) : ControllerBase
{
    private const string Language = "{languageCode:length(2)}";

    [HttpGet]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<PagedResult<ContentSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List(
        [FromQuery] string? type, [FromQuery] string? status, [FromQuery] bool? isLive, [FromQuery] bool needsUrlDecision, [FromQuery] Guid? parentId,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        this.OkOrProblem(await administration.ListAsync(type, status, isLive, needsUrlDecision, parentId, search, page, pageSize, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Content.Create)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create(CreateContentRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await administration.CreateAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await administration.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}/translations/" + Language)]
    [HasPermission(Permissions.Content.Edit)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> UpdateTranslation(Guid id, string languageCode, UpdateContentTranslationRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await administration.UpdateTranslationAsync(id, languageCode, request, cancellationToken));

    [HttpPut("{id:guid}/translations/" + Language + "/seo")]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateSeo(Guid id, string languageCode, UpdateContentSeoRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await administration.UpdateSeoAsync(id, languageCode, request, cancellationToken));

    [HttpPut("{id:guid}/settings")]
    [HasPermission(Permissions.Content.Edit)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateSettings(Guid id, UpdateContentSettingsRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await administration.UpdateSettingsAsync(id, request, cancellationToken));

    /// <summary>Permanent deletion (strong permission, audited). Former public URLs become explicit redirect or Gone rules.</summary>
    [HttpPost("{id:guid}/delete")]
    [HasPermission(Permissions.Content.DeletePermanent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeletePermanent(Guid id, DeleteContentRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await administration.DeletePermanentAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/submit")]
    [HasPermission(Permissions.Content.SubmitForReview)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Submit(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.SubmitForReviewAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    [HasPermission(Permissions.Content.Approve)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Approve(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.ApproveAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [HasPermission(Permissions.Content.Approve)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Reject(Guid id, RejectContentRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.RejectAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/publish")]
    [HasPermission(Permissions.Content.Publish)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Publish(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.PublishAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/schedule")]
    [HasPermission(Permissions.Content.Publish)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Schedule(Guid id, ScheduleContentRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.ScheduleAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/schedule/cancel")]
    [HasPermission(Permissions.Content.Publish)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CancelSchedule(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.CancelScheduleAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/unpublish-schedule")]
    [HasPermission(Permissions.Content.Publish)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ScheduleUnpublish(Guid id, ScheduleUnpublishRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.ScheduleUnpublishAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/unpublish-schedule/cancel")]
    [HasPermission(Permissions.Content.Publish)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CancelUnpublishSchedule(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.CancelUnpublishScheduleAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/unpublish")]
    [HasPermission(Permissions.Content.Publish)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Unpublish(Guid id, TakeOfflineRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.UnpublishAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/archive")]
    [HasPermission(Permissions.Content.Archive)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Archive(Guid id, TakeOfflineRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.ArchiveAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/restore")]
    [HasPermission(Permissions.Content.Archive)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Restore(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.RestoreFromArchiveAsync(id, request, cancellationToken));

    /// <summary>Decision for the URLs of content that was public and is now offline: a relevant replacement, or Gone.</summary>
    [HttpPost("{id:guid}/url-resolution")]
    [HasPermission(Permissions.Content.Publish)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ResolveOffline(Guid id, TakeOfflineRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.ResolveOfflineAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/quality")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<QualityReportResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Quality(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.GetQualityReportAsync(id, cancellationToken));

    [HttpGet("{id:guid}/publishing-time")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<PublishingTimeRecommendationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> PublishingTime(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workflow.RecommendPublishingTimeAsync(id, cancellationToken));

    [HttpGet("{id:guid}/revisions")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<IReadOnlyList<ContentRevisionSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Revisions(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await revisions.ListAsync(id, cancellationToken));

    [HttpGet("{id:guid}/revisions/{number:int}")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<ContentRevisionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Revision(Guid id, int number, CancellationToken cancellationToken) =>
        this.OkOrProblem(await revisions.GetAsync(id, number, cancellationToken));

    [HttpGet("{id:guid}/revisions/compare")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<RevisionComparisonResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Compare(Guid id, [FromQuery] int from, [FromQuery] int to, CancellationToken cancellationToken) =>
        this.OkOrProblem(await revisions.CompareAsync(id, from, to, cancellationToken));

    [HttpPost("{id:guid}/revisions/{number:int}/restore")]
    [HasPermission(Permissions.Content.Edit)]
    [ProducesResponseType<ContentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RestoreRevision(Guid id, int number, RestoreRevisionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await revisions.RestoreAsync(id, number, request, cancellationToken));

    [HttpPost("{id:guid}/preview-links")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<PreviewLinkResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CreatePreviewLink(Guid id, CreatePreviewLinkRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return this.OkOrProblem(await previews.CreateLinkAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/relations")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<IReadOnlyList<ContentRelationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Relations(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await administration.ListRelationsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/relations")]
    [HasPermission(Permissions.Content.Edit)]
    [ProducesResponseType<ContentRelationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> AddRelation(Guid id, AddContentRelationRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await administration.AddRelationAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}/relations/{relationId:guid}")]
    [HasPermission(Permissions.Content.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RemoveRelation(Guid id, Guid relationId, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await administration.RemoveRelationAsync(id, relationId, cancellationToken));

    [HttpGet("{id:guid}/related-suggestions")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<IReadOnlyList<RelatedSuggestionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RelatedSuggestions(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await administration.SuggestRelatedAsync(id, cancellationToken));

    [HttpGet("{id:guid}/intelligence/" + Language)]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<ContentIntelligenceResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Intelligence(Guid id, string languageCode, CancellationToken cancellationToken) =>
        this.OkOrProblem(await intelligence.GetContentProfileAsync(id, languageCode, cancellationToken));
}
