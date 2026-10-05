using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Leads;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;

namespace Onyx.API.Controllers.Admin;

/// <summary>The submission log. Read only: inquiries are immutable records. Lead visibility rules apply.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/inquiries")]
[HasPermission(Permissions.Inquiries.View)]
public sealed class AdminInquiriesController(ILeadAdministrationService leads) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<InquirySummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] InquiryListQuery query, CancellationToken cancellationToken) =>
        this.OkOrProblem(await leads.ListInquiriesAsync(query, cancellationToken));
}
