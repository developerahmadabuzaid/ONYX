using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Delivery;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// Delivery issues: search (only visible projects), details, create/edit, workflow transitions, board and backlog moves, comments,
/// links and watching. Project roles are enforced by <see cref="IssueService"/>; issues of invisible projects are 404.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/delivery/issues")]
[HasPermission(Permissions.Delivery.Access)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDeliveryIssuesController(
    IssueService issues,
    DeliveryQueryService queries,
    IssueAttachmentService attachments,
    IssueWorkLogService workLogs) : ControllerBase
{
    /// <summary>Multipart overhead allowance on top of the largest accepted attachment.</summary>
    public const long MaxAttachmentRequestBytes = IssueAttachmentService.MaxBytes + (1024 * 1024);

    [HttpGet]
    [ProducesResponseType<PagedResult<IssueListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<IssueListItemResponse>>> Search([FromQuery] IssueQuery query, CancellationToken cancellationToken) =>
        Ok(await queries.SearchAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create(SaveIssueRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update(Guid id, SaveIssueRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/transition")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Transition(Guid id, TransitionIssueRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.TransitionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/move")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Move(Guid id, MoveIssueRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.MoveAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await issues.DeleteAsync(id, cancellationToken));

    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AddComment(Guid id, SaveCommentRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.AddCommentAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/comments/{commentId:guid}")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> EditComment(Guid id, Guid commentId, SaveCommentRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.EditCommentAsync(id, commentId, request, cancellationToken));

    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.DeleteCommentAsync(id, commentId, cancellationToken));

    [HttpPost("{id:guid}/links")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> AddLink(Guid id, CreateIssueLinkRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.AddLinkAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}/links/{linkId:guid}")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveLink(Guid id, Guid linkId, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.RemoveLinkAsync(id, linkId, cancellationToken));

    [HttpPost("{id:guid}/dates")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Reschedule(Guid id, RescheduleIssueRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.RescheduleAsync(id, request, cancellationToken));

    /// <summary>One change for several issues; each is authorized and saved on its own and refusals are reported per issue.</summary>
    [HttpPost("bulk")]
    [ProducesResponseType<BulkIssueResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Bulk(BulkIssueRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.BulkAsync(request, cancellationToken));

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(MaxAttachmentRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAttachmentRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<IssueAttachmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Attach(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return this.Problem(Domain.Common.Error.Validation("Delivery.NoFile", "Choose a file to attach."));
        }

        await using var stream = file.OpenReadStream();
        return this.OkOrProblem(await attachments.UploadAsync(id, stream, file.FileName, cancellationToken));
    }

    /// <summary>The attached file, always offered as a download under its checked name and type.</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Attachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var file = await attachments.OpenAsync(id, attachmentId, cancellationToken);
        if (file.IsFailure)
        {
            return this.Problem(file.Error!);
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(file.Value.Content, file.Value.ContentType, file.Value.FileName);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await attachments.DeleteAsync(id, attachmentId, cancellationToken));

    [HttpPost("{id:guid}/worklogs")]
    [ProducesResponseType<WorkLogResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> LogWork(Guid id, SaveWorkLogRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await workLogs.AddAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}/worklogs/{workLogId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteWorkLog(Guid id, Guid workLogId, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await workLogs.DeleteAsync(id, workLogId, cancellationToken));

    [HttpPut("{id:guid}/watch")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Watch(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.SetWatchingAsync(id, watching: true, cancellationToken));

    [HttpDelete("{id:guid}/watch")]
    [ProducesResponseType<IssueDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Unwatch(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await issues.SetWatchingAsync(id, watching: false, cancellationToken));
}
