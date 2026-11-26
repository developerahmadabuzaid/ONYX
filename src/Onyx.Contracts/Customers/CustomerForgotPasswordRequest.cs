namespace Onyx.Contracts.Customers;

public sealed record CustomerForgotPasswordRequest(string Email, string Language, string? CaptchaToken);
