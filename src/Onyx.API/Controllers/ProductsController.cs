using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Http;
using Onyx.Application.Products;
using Onyx.Contracts.Common;
using Onyx.Contracts.Products;

namespace Onyx.API.Controllers;

/// <summary>Published SaaS products, plans and demos for visitors; no account needed.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/products")]
[AllowAnonymous]
public sealed class ProductsController(ProductCatalogService catalog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PublicProductListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicProductListItemResponse>>> List([FromQuery] string? lang, CancellationToken cancellationToken) =>
        Ok(await catalog.ListAsync(lang ?? "ar", cancellationToken));

    [HttpGet("{slug}")]
    [ProducesResponseType<PublicProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(string slug, [FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.OkOrProblem(await catalog.GetAsync(lang ?? "ar", slug, cancellationToken));
}
