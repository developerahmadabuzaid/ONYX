namespace Onyx.Contracts.Customers;

public sealed record CustomerSignInRequest(string Email, string Password, string? CaptchaToken);
