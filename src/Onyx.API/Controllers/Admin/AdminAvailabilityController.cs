using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Booking;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers.Admin;

/// <summary>Internal availability: booking settings, weekly windows and blocked dates.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/availability")]
[HasPermission(Permissions.Availability.Manage)]
public sealed class AdminAvailabilityController(IAvailabilityAdministrationService availability) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<BookingSettingsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingSettingsResponse>> Get(CancellationToken cancellationToken) => Ok(await availability.GetAsync(cancellationToken));

    [HttpPut("settings")]
    [ProducesResponseType<BookingSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SaveSettings(SaveBookingSettingsRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await availability.SaveSettingsAsync(request, cancellationToken));

    [HttpPost("rules")]
    [ProducesResponseType<BookingSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> AddRule(SaveAvailabilityRuleRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await availability.AddRuleAsync(request, cancellationToken));

    [HttpDelete("rules/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteRule(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await availability.DeleteRuleAsync(id, cancellationToken));

    [HttpPost("exceptions")]
    [ProducesResponseType<BookingSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> AddException(SaveAvailabilityExceptionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await availability.AddExceptionAsync(request, cancellationToken));

    [HttpDelete("exceptions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteException(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await availability.DeleteExceptionAsync(id, cancellationToken));
}
