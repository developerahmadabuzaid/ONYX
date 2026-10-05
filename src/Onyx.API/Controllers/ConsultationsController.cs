using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Onyx.API.Configuration;
using Onyx.API.Http;
using Onyx.Application.Booking;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers;

/// <summary>
/// Public consultation booking. The visitor manage token is always sent in the request body (never in a URL), and every
/// endpoint is rate limited per client IP.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/consultations")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Strict)]
public sealed class ConsultationsController(IBookingService booking) : ControllerBase
{
    [HttpGet("availability")]
    [ProducesResponseType<AvailabilityResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AvailabilityResponse>> Availability(CancellationToken cancellationToken) =>
        Ok(await booking.GetAvailabilityAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType<BookingConfirmedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> Book(BookConsultationRequest request, CancellationToken cancellationToken)
    {
        var result = await booking.BookAsync(request, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : this.Problem(result.Error!);
    }

    [HttpPost("manage")]
    [ProducesResponseType<ManagedBookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(ManageBookingRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await booking.GetManagedAsync(request, cancellationToken));

    [HttpPost("manage/cancel")]
    [ProducesResponseType<ManagedBookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Cancel(ManageBookingRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await booking.CancelAsync(request, cancellationToken));

    [HttpPost("manage/reschedule")]
    [ProducesResponseType<ManagedBookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Reschedule(RescheduleBookingRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await booking.RescheduleAsync(request, cancellationToken));

    [HttpPost("manage/calendar")]
    [ProducesResponseType<CalendarFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Calendar(ManageBookingRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await booking.GetCalendarFileAsync(request, cancellationToken));
}
