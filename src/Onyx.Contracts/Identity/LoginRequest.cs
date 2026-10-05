namespace Onyx.Contracts.Identity;

/// <summary><see cref="CaptchaToken"/> is the Turnstile response; the API requires it whenever Turnstile is configured.</summary>
public sealed record LoginRequest(string Email, string Password, string? CaptchaToken = null);
