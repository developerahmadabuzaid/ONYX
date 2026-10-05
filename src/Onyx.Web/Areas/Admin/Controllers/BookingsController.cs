using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Booking;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>Consultation bookings: upcoming or all, staff cancellation (optionally telling the visitor) and the outcome.</summary>
[Route("admin/bookings")]
public sealed class BookingsController(IAdminLeadsApiClient api) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(bool all = false, string? status = null, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await api.ListBookingsAsync(new BookingListQuery(!all, Blank(status), Math.Max(page, 1), 25), cancellationToken);
        if (Failed(result) is { } failed)
        {
            return failed;
        }

        ViewData["All"] = all;
        return View(result.Value);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, bool notifyVisitor, uint version, CancellationToken cancellationToken) =>
        After(await api.CancelBookingAsync(id, new CancelBookingRequest(notifyVisitor, version), cancellationToken), RedirectToAction(nameof(Index)));

    [HttpPost("{id:guid}/outcome")]
    public async Task<IActionResult> Outcome(Guid id, string outcome, uint version, CancellationToken cancellationToken) =>
        After(await api.RecordBookingOutcomeAsync(id, new RecordBookingOutcomeRequest(outcome ?? string.Empty, version), cancellationToken), RedirectToAction(nameof(Index), new { all = true }));
}
