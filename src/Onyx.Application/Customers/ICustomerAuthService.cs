using Onyx.Contracts.Customers;
using Onyx.Domain.Common;

namespace Onyx.Application.Customers;

/// <summary>
/// Customer sign-up, verification, sign-in and credentials, called server-to-server by Onyx.Web. Answers never reveal whether
/// an email address has an account.
/// </summary>
public interface ICustomerAuthService
{
    /// <summary>Succeeds for any valid request; a new account gets a verification email and an existing owner a notice instead.</summary>
    Task<Result> RegisterAsync(RegisterCustomerRequest request, CancellationToken cancellationToken);

    Task<Result> VerifyEmailAsync(VerifyCustomerEmailRequest request, CancellationToken cancellationToken);

    Task<Result> ResendVerificationAsync(ResendCustomerVerificationRequest request, CancellationToken cancellationToken);

    Task<Result<CustomerSessionResponse>> SignInAsync(CustomerSignInRequest request, CancellationToken cancellationToken);

    Task SignOutAsync(string sessionToken, CancellationToken cancellationToken);

    Task<Result> RequestPasswordResetAsync(CustomerForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<Result> ResetPasswordAsync(CustomerResetPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Ends every session of the customer; they sign in again with the new password.</summary>
    Task<Result> ChangePasswordAsync(Guid customerId, CustomerChangePasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Erases the account after re-checking the password; open subscriptions are cancelled.</summary>
    Task<Result> DeleteAccountAsync(Guid customerId, DeleteCustomerAccountRequest request, CancellationToken cancellationToken);

    /// <summary>Checks an opaque session token on every customer API request; null when unknown, expired or revoked.</summary>
    Task<CustomerSessionInfo?> ValidateSessionAsync(string sessionToken, CancellationToken cancellationToken);
}
