namespace Onyx.Infrastructure.Security;

/// <summary>
/// Cloudflare's published Turnstile test keys (https://developers.cloudflare.com/turnstile/troubleshooting/testing/). Their
/// verification always succeeds but reports a fixed action and hostname, so the strict action/hostname check can never pass
/// locally. That one check is relaxed only in Development and only for the always-pass test secret; Production refuses to
/// start with it.
/// </summary>
public static class TurnstileTestKeys
{
    public const string AlwaysPassesSecret = "1x0000000000000000000000000000000AA";

    public static bool RelaxesActionAndHostname(bool isDevelopment, string? secretKey) =>
        isDevelopment && string.Equals(secretKey, AlwaysPassesSecret, StringComparison.Ordinal);
}
