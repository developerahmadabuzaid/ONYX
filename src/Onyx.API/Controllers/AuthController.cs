using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Onyx.API.Configuration;
using Onyx.API.Http;
using Onyx.Application.Identity;
using Onyx.Contracts.Common;
using Onyx.Contracts.Identity;

namespace Onyx.API.Controllers;

/// <summary>
/// Admin authentication, called server-to-server by the Onyx.Web BFF only. Responses carrying tokens are never cached,
/// and tokens must never be passed on to browser JavaScript.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await auth.LoginAsync(request, cancellationToken));

    [HttpPost("login/two-factor")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType<TokenPairResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> CompleteTwoFactor(TwoFactorLoginRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await auth.CompleteTwoFactorLoginAsync(request, cancellationToken));

    [HttpPost("two-factor/enrollment/start")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType<TwoFactorEnrollmentStartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> StartEnrollment(TwoFactorEnrollmentStartRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await auth.StartTwoFactorEnrollmentAsync(request, cancellationToken));

    [HttpPost("two-factor/enrollment/complete")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType<TwoFactorEnrollmentCompleteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> CompleteEnrollment(TwoFactorEnrollmentCompleteRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await auth.CompleteTwoFactorEnrollmentAsync(request, cancellationToken));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<TokenPairResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await auth.RefreshAsync(request, cancellationToken));

    /// <summary>Always 204: logging out an unknown or already-revoked session reveals nothing.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Always 202 for well-formed input, whether or not the account exists.</summary>
    [HttpPost("password/forgot")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.RequestPasswordResetAsync(request, cancellationToken);
        return result.IsSuccess ? Accepted() : this.Problem(result.Error!);
    }

    [HttpPost("password/reset")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await auth.ResetPasswordAsync(request, cancellationToken));

    [HttpPost("invitations/accept")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AcceptInvitation(AcceptInvitationRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await auth.AcceptInvitationAsync(request, cancellationToken));

    /// <summary>Ends every session of the user (including this one); the BFF signs the user out.</summary>
    [HttpPost("password/change")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await auth.ChangePasswordAsync(request, cancellationToken));

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Me(CancellationToken cancellationToken) =>
        this.OkOrProblem(await auth.GetCurrentUserAsync(cancellationToken));
}
