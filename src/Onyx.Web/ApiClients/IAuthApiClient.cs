using Onyx.Contracts.Identity;

namespace Onyx.Web.ApiClients;

/// <summary>Anonymous (pre-session) authentication calls. Tokens returned here are kept server-side only.</summary>
public interface IAuthApiClient
{
    Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<ApiResult<TokenPairResponse>> CompleteTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken);

    Task<ApiResult<TwoFactorEnrollmentStartResponse>> StartEnrollmentAsync(TwoFactorEnrollmentStartRequest request, CancellationToken cancellationToken);

    Task<ApiResult<TwoFactorEnrollmentCompleteResponse>> CompleteEnrollmentAsync(TwoFactorEnrollmentCompleteRequest request, CancellationToken cancellationToken);

    Task<ApiResult<TokenPairResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken cancellationToken);

    /// <summary>Used once, right after sign-in, with the newly issued access token.</summary>
    Task<ApiResult<CurrentUserResponse>> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken);
}
