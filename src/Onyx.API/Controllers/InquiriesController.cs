using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Onyx.API.Configuration;
using Onyx.API.Http;
using Onyx.Application.Leads;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;

namespace Onyx.API.Controllers;

/// <summary>Public form submissions, sent server-to-server by Onyx.Web. Turnstile is verified here, server-side.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/inquiries")]
public sealed class InquiriesController(IInquiryService inquiries) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType<InquiryAcceptedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> Submit(SubmitInquiryRequest request, CancellationToken cancellationToken)
    {
        var result = await inquiries.SubmitAsync(request, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : this.Problem(result.Error!);
    }
}
