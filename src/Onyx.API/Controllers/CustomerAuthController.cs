using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Onyx.API.Configuration;
using Onyx.API.Http;
using Onyx.Application.Customers;
using Onyx.Contracts.Common;
using Onyx.Contracts.Customers;

namespace Onyx.API.Controllers;

/// <summary>
/// Customer sign-up, email verification, sign-in and password recovery, called server-to-server by Onyx.Web. Separate from the
/// staff <c>auth</c> resource; Turnstile is verified server-side; answers never reveal whether an address has an account.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/customer-auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Strict)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CustomerAuthController(ICustomerAuthService customers) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> Register(RegisterCustomerRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await customers.RegisterAsync(request, cancellationToken));

    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> VerifyEmail(VerifyCustomerEmailRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await customers.VerifyEmailAsync(request, cancellationToken));

    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResendVerification(ResendCustomerVerificationRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await customers.ResendVerificationAsync(request, cancellationToken));

    [HttpPost("sign-in")]
    [ProducesResponseType<CustomerSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> SignIn(CustomerSignInRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await customers.SignInAsync(request, cancellationToken));

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ForgotPassword(CustomerForgotPasswordRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await customers.RequestPasswordResetAsync(request, cancellationToken));

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResetPassword(CustomerResetPasswordRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await customers.ResetPasswordAsync(request, cancellationToken));
}
