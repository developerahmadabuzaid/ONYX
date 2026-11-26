using Onyx.Contracts.Customers;

namespace Onyx.Web.ApiClients;

/// <summary>Anonymous customer identity calls (sign-up, verification, sign-in, recovery).</summary>
public interface ICustomerAuthApiClient
{
    Task<ApiResult<NoContent>> RegisterAsync(RegisterCustomerRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> VerifyEmailAsync(VerifyCustomerEmailRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> ResendVerificationAsync(ResendCustomerVerificationRequest request, CancellationToken cancellationToken);

    Task<ApiResult<CustomerSessionResponse>> SignInAsync(CustomerSignInRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> ForgotPasswordAsync(CustomerForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> ResetPasswordAsync(CustomerResetPasswordRequest request, CancellationToken cancellationToken);
}
