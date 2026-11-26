using Onyx.Contracts.Customers;

namespace Onyx.Web.ApiClients;

public sealed class CustomerAuthApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), ICustomerAuthApiClient
{
    private const string Base = "api/v1/customer-auth";

    public Task<ApiResult<NoContent>> RegisterAsync(RegisterCustomerRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"{Base}/register", request, cancellationToken);

    public Task<ApiResult<NoContent>> VerifyEmailAsync(VerifyCustomerEmailRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"{Base}/verify-email", request, cancellationToken);

    public Task<ApiResult<NoContent>> ResendVerificationAsync(ResendCustomerVerificationRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"{Base}/resend-verification", request, cancellationToken);

    public Task<ApiResult<CustomerSessionResponse>> SignInAsync(CustomerSignInRequest request, CancellationToken cancellationToken) =>
        PostAsync<CustomerSessionResponse>($"{Base}/sign-in", request, cancellationToken);

    public Task<ApiResult<NoContent>> ForgotPasswordAsync(CustomerForgotPasswordRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"{Base}/forgot-password", request, cancellationToken);

    public Task<ApiResult<NoContent>> ResetPasswordAsync(CustomerResetPasswordRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"{Base}/reset-password", request, cancellationToken);
}
