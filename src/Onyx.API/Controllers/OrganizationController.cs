using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onyx.Application.Seo;
using Onyx.Contracts.Common;
using Onyx.Contracts.Organization;

namespace Onyx.API.Controllers;

/// <summary>Owner-verified organization facts for structured data. 404 until every required fact is verified and the owner enabled it.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/organization")]
public sealed class OrganizationController(IOrganizationProfileService organization) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PublicOrganizationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicOrganizationResponse>> Get(CancellationToken cancellationToken) =>
        await organization.GetVerifiedAsync(cancellationToken) is { } verified ? Ok(verified) : NotFound();
}
