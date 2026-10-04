using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Intelligence;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Intelligence;

namespace Onyx.API.Controllers.Admin;

/// <summary>Content calendar (content engine permissions): proposals are reviewed by admins; nothing is scheduled automatically.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/content-calendar")]
public sealed class AdminContentCalendarController(IContentCalendarService calendar) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ContentEngine.View)]
    [ProducesResponseType<PagedResult<CalendarEntryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        this.OkOrProblem(await calendar.ListAsync(status, page, pageSize, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.ContentEngine.Review)]
    [ProducesResponseType<CalendarEntryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Propose(ProposeCalendarEntryRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await calendar.ProposeAsync(request, cancellationToken));

    [HttpPost("{id:guid}/accept")]
    [HasPermission(Permissions.ContentEngine.Approve)]
    [ProducesResponseType<CalendarEntryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Accept(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await calendar.AcceptAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [HasPermission(Permissions.ContentEngine.Approve)]
    [ProducesResponseType<CalendarEntryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Reject(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await calendar.RejectAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/link")]
    [HasPermission(Permissions.ContentEngine.Review)]
    [ProducesResponseType<CalendarEntryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Link(Guid id, LinkCalendarContentRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await calendar.LinkContentAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/complete")]
    [HasPermission(Permissions.ContentEngine.Review)]
    [ProducesResponseType<CalendarEntryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Complete(Guid id, DecideRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await calendar.CompleteAsync(id, request, cancellationToken));
}
