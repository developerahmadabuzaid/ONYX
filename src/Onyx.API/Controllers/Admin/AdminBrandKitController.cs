using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Brand;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Brand;

namespace Onyx.API.Controllers.Admin;

/// <summary>The Onyx Brand Kit. Changing it is a sensitive, audited action (BrandKit.Manage; SuperAdmin by default).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/brand-kit")]
public sealed class AdminBrandKitController(IBrandKitService brandKit) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.BrandKit.View)]
    [ProducesResponseType<BrandKitResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BrandKitResponse>> Get(CancellationToken cancellationToken) => Ok(await brandKit.GetAsync(cancellationToken));

    [HttpPut]
    [HasPermission(Permissions.BrandKit.Manage)]
    [ProducesResponseType<BrandKitResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(UpdateBrandKitRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await brandKit.UpdateAsync(request, cancellationToken));
}
