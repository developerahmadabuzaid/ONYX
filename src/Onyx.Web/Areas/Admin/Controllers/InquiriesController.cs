using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Leads;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>The submission log (read only). Each row links to its lead, where the full inquiry and its origin are shown.</summary>
[Route("admin/inquiries")]
public sealed class InquiriesController(IAdminLeadsApiClient api) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? type, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await api.ListInquiriesAsync(new InquiryListQuery(Blank(type), Math.Max(page, 1), 25), cancellationToken);
        return Failed(result) ?? View(result.Value);
    }
}
