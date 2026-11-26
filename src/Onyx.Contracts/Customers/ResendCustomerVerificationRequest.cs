namespace Onyx.Contracts.Customers;

public sealed record ResendCustomerVerificationRequest(string Email, string Language, string? ReturnPath, string? CaptchaToken);
