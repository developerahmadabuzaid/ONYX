using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Media;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Media;

namespace Onyx.API.Controllers.Admin;

/// <summary>Media library. Uploads are verified by content, size- and dimension-limited, and stored under random keys.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/media")]
public sealed class AdminMediaController(IMediaService media) : ControllerBase
{
    /// <summary>Multipart overhead allowance on top of the largest accepted file.</summary>
    public const long MaxUploadRequestBytes = 11 * 1024 * 1024;

    [HttpGet]
    [HasPermission(Permissions.Media.View)]
    [ProducesResponseType<PagedResult<MediaAssetResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MediaAssetResponse>>> List(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await media.ListAsync(search, page, pageSize, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Media.Upload)]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<MediaAssetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return this.Problem(Domain.Common.Error.Validation("Media.NoFile", "Choose a file to upload."));
        }

        await using var stream = file.OpenReadStream();
        return this.OkOrProblem(await media.UploadAsync(stream, file.FileName, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Media.View)]
    [ProducesResponseType<MediaAssetResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await media.GetAsync(id, cancellationToken));

    [HttpGet("{id:guid}/usages")]
    [HasPermission(Permissions.Media.View)]
    [ProducesResponseType<IReadOnlyList<MediaUsageResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Usages(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await media.ListUsagesAsync(id, cancellationToken));

    [HttpPut("{id:guid}/texts")]
    [HasPermission(Permissions.Media.Edit)]
    [ProducesResponseType<MediaAssetResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateTexts(Guid id, UpdateMediaTextsRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await media.UpdateTextsAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Media.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await media.DeleteAsync(id, cancellationToken));

    /// <summary>The file itself, for admin previews (any asset, used or not).</summary>
    [HttpGet("{id:guid}/content")]
    [HasPermission(Permissions.Media.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Content(Guid id, CancellationToken cancellationToken)
    {
        var file = await media.OpenAdminAsync(id, cancellationToken);
        if (file.IsFailure)
        {
            return this.Problem(file.Error!);
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(file.Value.Content, file.Value.ContentType);
    }
}
