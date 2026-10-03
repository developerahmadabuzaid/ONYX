using Onyx.Contracts.Identity;
using Onyx.Domain.Common;

namespace Onyx.Application.Identity;

/// <summary>
/// Admin authentication for the Onyx.Web BFF (server-to-server). Failures that could reveal whether an account exists
/// return the same generic error.
/// </summary>
public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<TokenPairResponse>> CompleteTwoFactorLoginAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken);

    Task<Result<TwoFactorEnrollmentStartResponse>> StartTwoFactorEnrollmentAsync(TwoFactorEnrollmentStartRequest request, CancellationToken cancellationToken);

    Task<Result<TwoFactorEnrollmentCompleteResponse>> CompleteTwoFactorEnrollmentAsync(TwoFactorEnrollmentCompleteRequest request, CancellationToken cancellationToken);

    Task<Result<TokenPairResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);

    /// <summary>Always succeeds from the caller's point of view; an email is queued only for an active, confirmed account.</summary>
    Task<Result> RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);

    Task<Result> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken cancellationToken);

    /// <summary>Revokes every session of the user, including the current one; the user signs in again.</summary>
    Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken);

    Task<Result<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken);
}
