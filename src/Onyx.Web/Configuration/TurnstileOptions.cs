using System.ComponentModel.DataAnnotations;

namespace Onyx.Web.Configuration;

/// <summary>
/// Bound from <c>Turnstile</c>. The site key is public (it is rendered into form pages); the secret key belongs to Onyx.API only.
/// Without a site key the public forms explain that online submission is unavailable instead of offering a form that would fail.
/// </summary>
public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";

    /// <summary>The only third-party origin a page may use, and only on pages that render the challenge.</summary>
    public const string Origin = "https://challenges.cloudflare.com";

    public const string ScriptUrl = Origin + "/turnstile/v0/api.js";

    [RegularExpression("^[A-Za-z0-9_-]{0,100}$")]
    public string SiteKey { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SiteKey);
}
