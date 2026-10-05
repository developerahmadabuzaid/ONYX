using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.Security;

/// <summary>
/// Cloudflare Turnstile server-side verification (Onyx.API). <see cref="SecretKey"/> lives only in the secret store (User Secrets
/// locally). Without a secret every protected submission is refused: anti-abuse verification fails closed.
/// </summary>
public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Hostnames the widget may be solved on. Empty means the hosts of <c>Site:PublicBaseUrl</c> and <c>IdentitySecurity:AdminPortalBaseUrl</c>.</summary>
    public IReadOnlyList<string> AllowedHostnames { get; set; } = [];

    [Required]
    public string VerifyEndpoint { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Tokens older than this are refused even if the provider still accepts them.</summary>
    [Range(30, 300)]
    public int MaxTokenAgeSeconds { get; set; } = 300;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);

    /// <summary>
    /// The configured hostnames, or else the hosts of the public site (forms) and the admin portal (sign-in, password reset).
    /// </summary>
    public HashSet<string> ResolveHostnames(string? publicBaseUrl, string? adminPortalBaseUrl)
    {
        var configured = AllowedHostnames.Where(host => !string.IsNullOrWhiteSpace(host)).Select(host => host.Trim().ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        if (configured.Count == 0)
        {
            foreach (var url in new[] { publicBaseUrl, adminPortalBaseUrl })
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var origin))
                {
                    configured.Add(origin.IdnHost.ToLowerInvariant());
                }
            }
        }

        return configured;
    }

    /// <summary>
    /// Production requires Turnstile (owner decision after Stage 8): a secret plus at least one hostname. Other environments may
    /// run without keys (public forms then fail closed; admin sign-in keeps rate limit, lockout and 2FA).
    /// </summary>
    public bool IsValidFor(bool isProduction, string? publicBaseUrl, string? adminPortalBaseUrl) =>
        !isProduction || (IsConfigured && ResolveHostnames(publicBaseUrl, adminPortalBaseUrl).Count > 0);
}
