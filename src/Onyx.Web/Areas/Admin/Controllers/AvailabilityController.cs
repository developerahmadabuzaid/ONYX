using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Booking;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Internal availability. Working days, hours, slot length, buffer, notice and horizon are business decisions: nothing is
/// preset, and online booking stays closed until they are entered and booking is enabled.
/// </summary>
[Route("admin/availability")]
public sealed class AvailabilityController(IAdminLeadsApiClient api) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await api.GetAvailabilityAsync(cancellationToken);
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("settings")]
    public async Task<IActionResult> Settings(
        bool isEnabled, string timeZoneId, int slotMinutes, int bufferMinutes, int minimumNoticeHours, int horizonDays, uint version, CancellationToken cancellationToken) =>
        After(
            await api.SaveBookingSettingsAsync(
                new SaveBookingSettingsRequest(isEnabled, timeZoneId ?? string.Empty, slotMinutes, bufferMinutes, minimumNoticeHours, horizonDays, version), cancellationToken),
            RedirectToAction(nameof(Index)));

    [HttpPost("rules")]
    public async Task<IActionResult> AddRule(string dayOfWeek, string startTime, string endTime, CancellationToken cancellationToken) =>
        After(
            await api.AddAvailabilityRuleAsync(new SaveAvailabilityRuleRequest(dayOfWeek ?? string.Empty, startTime ?? string.Empty, endTime ?? string.Empty), cancellationToken),
            RedirectToAction(nameof(Index)),
            "Cms.Created");

    [HttpPost("rules/{id:guid}/delete")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken cancellationToken) =>
        After(await api.DeleteAvailabilityRuleAsync(id, cancellationToken), RedirectToAction(nameof(Index)), "Cms.Deleted");

    [HttpPost("exceptions")]
    public async Task<IActionResult> AddException(DateOnly date, string? reason, CancellationToken cancellationToken) =>
        After(await api.AddAvailabilityExceptionAsync(new SaveAvailabilityExceptionRequest(date, Blank(reason)), cancellationToken), RedirectToAction(nameof(Index)), "Cms.Created");

    [HttpPost("exceptions/{id:guid}/delete")]
    public async Task<IActionResult> DeleteException(Guid id, CancellationToken cancellationToken) =>
        After(await api.DeleteAvailabilityExceptionAsync(id, cancellationToken), RedirectToAction(nameof(Index)), "Cms.Deleted");
}
