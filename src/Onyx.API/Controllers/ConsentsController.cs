using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Onyx.API.Configuration;
using Onyx.API.Http;
using Onyx.Application.Privacy;
using Onyx.Contracts.Common;
using Onyx.Contracts.Privacy;

namespace Onyx.API.Controllers;

/// <summary>Cookie consent receipts, recorded server-to-server by Onyx.Web on behalf of anonymous visitors.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/consents")]
public sealed class ConsentsController(ICookieConsentService consents) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType<AcceptedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Record(RecordCookieConsentRequest request, CancellationToken cancellationToken)
    {
        var result = await consents.RecordAsync(request, cancellationToken);
        return result.IsSuccess ? Accepted(new AcceptedResponse(result.Value)) : this.Problem(result.Error!);
    }
}
