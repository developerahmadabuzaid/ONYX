using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Subscriptions;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Commerce;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers.Admin;

/// <summary>Subscription requests and trials: review and decide (activate, reject, cancel).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/subscriptions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminSubscriptionsController(SubscriptionAdministrationService subscriptions) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Subscriptions.View)]
    [ProducesResponseType<PagedResult<AdminSubscriptionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminSubscriptionResponse>>> List(
        [FromQuery] string? status, [FromQuery] string? lang, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await subscriptions.ListAsync(status, lang ?? "en", page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Subscriptions.View)]
    [ProducesResponseType<AdminSubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, [FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.OkOrProblem(await subscriptions.GetAsync(id, lang ?? "en", cancellationToken));

    [HttpPost("{id:guid}/decision")]
    [HasPermission(Permissions.Subscriptions.Manage)]
    [ProducesResponseType<AdminSubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Decide(Guid id, SubscriptionDecisionRequest request, [FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.OkOrProblem(await subscriptions.DecideAsync(id, request, lang ?? "en", cancellationToken));
}
