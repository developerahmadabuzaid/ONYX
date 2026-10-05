using System.Text;
using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Privacy;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Data-subject requests: receive, verify, reject, export (downloaded straight through, never stored) and erase (the address
/// must be typed again). All pages are no-store / no-referrer (<see cref="CmsControllerBase"/>); the API authorizes every call.
/// </summary>
[Route("admin/privacy/requests")]
public sealed class PrivacyRequestsController(IAdminPrivacyApiClient api) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await api.ListRequestsAsync(Blank(status), Math.Max(page, 1), cancellationToken);
        if (Failed(result) is { } failed)
        {
            return failed;
        }

        ViewData["Status"] = Blank(status);
        return View(result.Value);
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(string type, string subjectEmail, string? note, CancellationToken cancellationToken)
    {
        var result = await api.CreateRequestAsync(new CreateDataSubjectRequestRequest(type ?? string.Empty, subjectEmail ?? string.Empty, Blank(note)), cancellationToken);
        return result.IsSuccess
            ? After(result, RedirectToAction(nameof(Details), new { id = result.Value!.Id }), "Cms.Created")
            : After(result, RedirectToAction(nameof(Index)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await api.GetRequestAsync(id, cancellationToken);
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, string verificationNote, uint version, CancellationToken cancellationToken) =>
        After(await api.VerifyRequestAsync(id, new VerifyDataSubjectRequestRequest(verificationNote ?? string.Empty, version), cancellationToken), RedirectToAction(nameof(Details), new { id }));

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, string reason, uint version, CancellationToken cancellationToken) =>
        After(await api.RejectRequestAsync(id, new RejectDataSubjectRequestRequest(reason ?? string.Empty, version), cancellationToken), RedirectToAction(nameof(Details), new { id }));

    /// <summary>Streams the export to the operator; nothing is written to disk or kept by Onyx.Web.</summary>
    [HttpPost("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var result = await api.ExportAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return After(result, RedirectToAction(nameof(Details), new { id }));
        }

        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return File(Encoding.UTF8.GetBytes(result.Value!.Content), "application/json; charset=utf-8", result.Value.FileName);
    }

    [HttpPost("{id:guid}/erase")]
    public async Task<IActionResult> Erase(Guid id, string confirmSubjectEmail, CancellationToken cancellationToken) =>
        After(await api.EraseAsync(id, new ConfirmErasureRequest(confirmSubjectEmail ?? string.Empty), cancellationToken), RedirectToAction(nameof(Details), new { id }), "Privacy.Erased");
}
