using System.Text.RegularExpressions;

namespace Onyx.Infrastructure.Analytics;

/// <summary>
/// Bound from <c>Analytics:Google</c>. Everything is empty by default: without the owner's property, site and service-account
/// key both providers stay "not configured". The key is a secret (secret store or environment only, never appsettings in
/// source control); the ids are not secret.
/// </summary>
public sealed partial class GoogleAnalyticsOptions
{
    public const string SectionName = "Analytics:Google";

    /// <summary>GA4 property id (digits only), e.g. from the GA4 admin "Property details".</summary>
    public string? Ga4PropertyId { get; set; }

    /// <summary>Search Console property: <c>sc-domain:example.com</c> or a URL-prefix property such as <c>https://www.example.com/</c>.</summary>
    public string? SearchConsoleSiteUrl { get; set; }

    /// <summary>Service-account key JSON (secret). Alternatively <see cref="ServiceAccountKeyFile"/>.</summary>
    public string? ServiceAccountKeyJson { get; set; }

    /// <summary>Path of a service-account key file mounted by the host (secret, never in the repository).</summary>
    public string? ServiceAccountKeyFile { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 60;

    public bool HasCredentials => !string.IsNullOrWhiteSpace(ServiceAccountKeyJson) || !string.IsNullOrWhiteSpace(ServiceAccountKeyFile);

    public bool Ga4Configured => HasCredentials && IsValidPropertyId(Ga4PropertyId);

    public bool SearchConsoleConfigured => HasCredentials && IsValidSiteUrl(SearchConsoleSiteUrl);

    public static bool IsValidPropertyId(string? value) => value is { Length: > 0 and <= 20 } && value.All(char.IsAsciiDigit);

    public static bool IsValidSiteUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith("sc-domain:", StringComparison.Ordinal))
        {
            return DomainPattern().IsMatch(value["sc-domain:".Length..]);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && value.EndsWith('/') && string.IsNullOrEmpty(uri.UserInfo);
    }

    /// <summary>Settings are either absent (not configured) or well-formed: a typo must not look like "not configured".</summary>
    public bool IsWellFormed() =>
        (string.IsNullOrEmpty(Ga4PropertyId) || IsValidPropertyId(Ga4PropertyId))
        && (string.IsNullOrEmpty(SearchConsoleSiteUrl) || IsValidSiteUrl(SearchConsoleSiteUrl))
        && RequestTimeoutSeconds is >= 5 and <= 300;

    [GeneratedRegex(@"^(?=.{1,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex DomainPattern();
}
