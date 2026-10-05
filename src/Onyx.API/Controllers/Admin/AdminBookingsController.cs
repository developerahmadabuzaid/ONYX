using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Booking;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/bookings")]
public sealed class AdminBookingsController(IBookingAdministrationService bookings) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Bookings.View)]
    [ProducesResponseType<PagedResult<BookingResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] BookingListQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await bookings.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Bookings.View)]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await bookings.GetAsync(id, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.Bookings.Manage)]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Cancel(Guid id, CancelBookingRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await bookings.CancelAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/outcome")]
    [HasPermission(Permissions.Bookings.Manage)]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RecordOutcome(Guid id, RecordBookingOutcomeRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await bookings.RecordOutcomeAsync(id, request, cancellationToken));
}
