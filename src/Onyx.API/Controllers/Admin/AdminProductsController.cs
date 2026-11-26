using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Products;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Products;

namespace Onyx.API.Controllers.Admin;

/// <summary>The SaaS product catalogue: products, plans, prices, demos, verification and publishing.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/products")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminProductsController(ProductAdministrationService products) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Products.View)]
    [ProducesResponseType<IReadOnlyList<AdminProductListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminProductListItemResponse>>> List([FromQuery] string? lang, CancellationToken cancellationToken) =>
        Ok(await products.ListAsync(lang ?? "en", cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Products.View)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await products.GetAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Products.Edit)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create(CreateProductRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await products.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Products.Edit)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Save(Guid id, SaveProductRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await products.SaveAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/plans")]
    [HasPermission(Permissions.Products.Edit)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> AddPlan(Guid id, SavePlanRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await products.AddPlanAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/plans/{planId:guid}")]
    [HasPermission(Permissions.Products.Edit)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> UpdatePlan(Guid id, Guid planId, SavePlanRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await products.UpdatePlanAsync(id, planId, request, cancellationToken));

    [HttpPost("{id:guid}/verify")]
    [HasPermission(Permissions.Products.Publish)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Verify(Guid id, VerifyProductRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await products.VerifyAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/publish")]
    [HasPermission(Permissions.Products.Publish)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Publish(Guid id, ProductVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await products.PublishAsync(id, request, publish: true, cancellationToken));

    [HttpPost("{id:guid}/unpublish")]
    [HasPermission(Permissions.Products.Publish)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Unpublish(Guid id, ProductVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await products.PublishAsync(id, request, publish: false, cancellationToken));
}
