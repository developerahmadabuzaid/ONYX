using Onyx.Domain.Common;

namespace Onyx.Application.Abstractions;

/// <summary>
/// Server-side anti-abuse verification (Cloudflare Turnstile, CLAUDE.md §14). Checks the token with the provider, the expected
/// action and the site hostname; tokens are single use. Fails closed: when the verifier is not configured or the provider
/// cannot be reached, the submission is refused (<see cref="ErrorType.Unavailable"/>), never let through.
/// </summary>
public interface ICaptchaVerifier
{
    /// <summary>A secret and at least one allowed hostname are configured.</summary>
    bool IsConfigured { get; }

    Task<Result> VerifyAsync(string? token, string expectedAction, string? remoteIp, CancellationToken cancellationToken);
}
