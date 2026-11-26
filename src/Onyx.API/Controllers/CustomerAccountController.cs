using System.Net.Http.Headers;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Onyx.API.Authentication;
using Onyx.API.Configuration;
using Onyx.API.Http;
using Onyx.Application.Customers;
using Onyx.Application.Products;
using Onyx.Contracts.Common;
using Onyx.Contracts.Customers;

namespace Onyx.API.Controllers;

/// <summary>
/// The signed-in customer's own account: profile, credentials, subscriptions, invoices and the AI demo. Authenticated by the
/// customer scheme only; staff tokens are not accepted. Every query is scoped to the caller.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/customer")]
[Authorize(Policy = CustomerAuthentication.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CustomerAccountController(
    CustomerPortalService portal, ICustomerAuthService customers, AiDemoService demos, ICurrentCustomer current) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<CustomerOverviewResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Overview([FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.OkOrProblem(await portal.OverviewAsync(lang ?? "ar", cancellationToken));

    [HttpGet("profile")]
    [ProducesResponseType<CustomerProfileResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Profile(CancellationToken cancellationToken) =>
        this.OkOrProblem(await portal.ProfileAsync(cancellationToken));

    [HttpPut("profile")]
    [ProducesResponseType<CustomerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> UpdateProfile(UpdateCustomerProfileRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await portal.UpdateProfileAsync(request, cancellationToken));

    /// <summary>Ends every session of the customer, including this one.</summary>
    [HttpPost("password")]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangePassword(CustomerChangePasswordRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await customers.ChangePasswordAsync(current.CustomerId!.Value, request, cancellationToken));

    [HttpPost("delete")]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteAccount(DeleteCustomerAccountRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await customers.DeleteAccountAsync(current.CustomerId!.Value, request, cancellationToken));

    [HttpPost("sign-out")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> SignOut(CancellationToken cancellationToken)
    {
        if (AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header) && header.Parameter is { } token)
        {
            await customers.SignOutAsync(token, cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("subscriptions")]
    [ProducesResponseType<IReadOnlyList<CustomerSubscriptionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Subscriptions([FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.OkOrProblem(await portal.SubscriptionsAsync(lang ?? "ar", cancellationToken));

    [HttpPost("subscriptions")]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType<CustomerSubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> StartSubscription(StartSubscriptionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await portal.StartSubscriptionAsync(request, cancellationToken));

    [HttpPost("subscriptions/{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CancelSubscription(Guid id, CancelSubscriptionRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await portal.CancelSubscriptionAsync(id, request, cancellationToken));

    [HttpGet("invoices")]
    [ProducesResponseType<IReadOnlyList<CustomerInvoiceResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Invoices(CancellationToken cancellationToken) =>
        this.OkOrProblem(await portal.InvoicesAsync(cancellationToken));

    [HttpGet("invoices/{id:guid}")]
    [ProducesResponseType<CustomerInvoiceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Invoice(Guid id, CancellationToken cancellationToken) =>
        this.OkOrProblem(await portal.InvoiceAsync(id, cancellationToken));

    [HttpPost("ai-demo/runs")]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType<AiDemoRunResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> StartDemo(StartAiDemoRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await demos.StartAsync(request, cancellationToken));

    [HttpGet("ai-demo/runs/{id:guid}")]
    [ProducesResponseType<AiDemoRunResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DemoRun(Guid id, [FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.OkOrProblem(await demos.GetAsync(id, lang ?? "ar", cancellationToken));

    [HttpGet("ai-demo/products/{slug}/runs")]
    [ProducesResponseType<IReadOnlyList<AiDemoRunResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DemoRuns(string slug, [FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.OkOrProblem(await demos.RecentAsync(slug, lang ?? "ar", cancellationToken));
}
