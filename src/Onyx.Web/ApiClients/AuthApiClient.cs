using Onyx.Contracts.Identity;

namespace Onyx.Web.ApiClients;

public sealed class AuthApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IAuthApiClient
{
    private const string Base = "api/v1/auth/";

    public Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        PostAsync<LoginResponse>(Base + "login", request, cancellationToken);

    public Task<ApiResult<TokenPairResponse>> CompleteTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken) =>
        PostAsync<TokenPairResponse>(Base + "login/two-factor", request, cancellationToken);

    public Task<ApiResult<TwoFactorEnrollmentStartResponse>> StartEnrollmentAsync(TwoFactorEnrollmentStartRequest request, CancellationToken cancellationToken) =>
        PostAsync<TwoFactorEnrollmentStartResponse>(Base + "two-factor/enrollment/start", request, cancellationToken);

    public Task<ApiResult<TwoFactorEnrollmentCompleteResponse>> CompleteEnrollmentAsync(TwoFactorEnrollmentCompleteRequest request, CancellationToken cancellationToken) =>
        PostAsync<TwoFactorEnrollmentCompleteResponse>(Base + "two-factor/enrollment/complete", request, cancellationToken);

    public Task<ApiResult<TokenPairResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken) =>
        PostAsync<TokenPairResponse>(Base + "refresh", request, cancellationToken);

    public Task<ApiResult<NoContent>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>(Base + "logout", request, cancellationToken);

    public Task<ApiResult<NoContent>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>(Base + "password/forgot", request, cancellationToken);

    public Task<ApiResult<NoContent>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>(Base + "password/reset", request, cancellationToken);

    public Task<ApiResult<NoContent>> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>(Base + "invitations/accept", request, cancellationToken);

    public Task<ApiResult<CurrentUserResponse>> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken) =>
        GetAsync<CurrentUserResponse>(Base + "me", cancellationToken, accessToken);
}
