using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Intelligence;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Intelligence;

namespace Onyx.API.Controllers.Admin;

/// <summary>Keyword &amp; Content Map: targeting decisions only (no fabricated volume, difficulty or rankings).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/keywords")]
public sealed class AdminKeywordsController(IKeywordMapService keywords) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<PagedResult<KeywordTargetResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List(
        [FromQuery] string? language, [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        this.OkOrProblem(await keywords.ListAsync(language, status, search, page, pageSize, cancellationToken));

    [HttpGet("conflicts")]
    [HasPermission(Permissions.Content.View)]
    [ProducesResponseType<KeywordConflictResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Conflicts([FromQuery] string language, [FromQuery] string keyword, [FromQuery] Guid? excludeContentItemId, CancellationToken cancellationToken) =>
        this.OkOrProblem(await keywords.CheckConflictsAsync(language ?? string.Empty, keyword ?? string.Empty, excludeContentItemId, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType<KeywordTargetResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Create(SaveKeywordTargetRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await keywords.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType<KeywordTargetResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, SaveKeywordTargetRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await keywords.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Seo.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await keywords.DeleteAsync(id, cancellationToken));
}
