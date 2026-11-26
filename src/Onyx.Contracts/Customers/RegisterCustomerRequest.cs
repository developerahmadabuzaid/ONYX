namespace Onyx.Contracts.Customers;

/// <summary>Public sign-up. <c>ReturnPath</c> is a local path the verification link brings the customer back to (validated by the API).</summary>
public sealed record RegisterCustomerRequest(string Email, string Password, string DisplayName, string? Company, string Language, bool AcceptTerms, bool MarketingConsent, string? ReturnPath, string? CaptchaToken);
