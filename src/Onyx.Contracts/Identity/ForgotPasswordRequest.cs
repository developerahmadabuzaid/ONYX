namespace Onyx.Contracts.Identity;

/// <summary><see cref="CaptchaToken"/> is the Turnstile response; the API requires it whenever Turnstile is configured.</summary>
public sealed record ForgotPasswordRequest(string Email, string? CaptchaToken = null);
