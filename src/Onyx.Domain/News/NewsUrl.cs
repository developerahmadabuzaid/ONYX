using System.Security.Cryptography;
using System.Text;
using Onyx.Domain.Common;

namespace Onyx.Domain.News;

/// <summary>
/// Syntax rules for external news URLs (feeds, homepages and article links) and their canonical form for deduplication. Only
/// absolute http(s) URLs on a public-looking domain name pass: no credentials, no IP literals or numeric hosts, no internal
/// names (localhost, *.local, *.internal…), default ports only. These checks are a first gate; the network-level guard
/// (resolved addresses, redirects) is enforced again by SafeOutboundHttp at fetch time.
/// </summary>
public static class NewsUrl
{
    public const int MaxLength = 2_000;
    public const int HashLength = 64;

    private static readonly string[] InternalSuffixes =
        [".localhost", ".local", ".localdomain", ".internal", ".intranet", ".lan", ".home", ".home.arpa", ".corp", ".private", ".test", ".invalid", ".example", ".onion", ".arpa"];

    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "fbclid", "gclid", "dclid", "gbraid", "wbraid", "msclkid", "mc_cid", "mc_eid", "igshid", "yclid", "_ga", "_gl", "ref_src", "cmpid",
    };

    /// <summary>Parses and checks <paramref name="value"/>; <paramref name="requireHttps"/> refuses plain http.</summary>
    public static Result<Uri> Parse(string? value, bool requireHttps)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return Invalid("Enter an absolute URL.");
        }

        return Check(uri, requireHttps);
    }

    /// <summary>The same checks for an already parsed URL (for example a link found inside a feed).</summary>
    public static Result<Uri> Check(Uri uri, bool requireHttps)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.OriginalString.Length > MaxLength)
        {
            return Invalid("Enter an absolute URL.");
        }

        if (uri.Scheme != Uri.UriSchemeHttps && (requireHttps || uri.Scheme != Uri.UriSchemeHttp))
        {
            return Invalid(requireHttps ? "Only https URLs are allowed." : "Only http and https URLs are allowed.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return Invalid("URLs must not contain user names or passwords.");
        }

        if (!uri.IsDefaultPort)
        {
            return Invalid("Only the standard port is allowed.");
        }

        return IsPublicHostName(uri) ? uri : Invalid("Only public domain names are allowed.");
    }

    /// <summary>
    /// True for a DNS name that could be public: not an IP literal or numeric host, at least two labels, and not a reserved or
    /// internal name. Resolution to a public address is still checked when connecting.
    /// </summary>
    public static bool IsPublicHostName(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.HostNameType != UriHostNameType.Dns)
        {
            return false;
        }

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        var labels = host.Split('.');
        if (labels.Length < 2 || labels.Any(label => label.Length == 0))
        {
            return false;
        }

        // A numeric or hex last label (2130706433, 0x7f.1, 127.1) is an address in disguise; no real top-level domain is numeric.
        var last = labels[^1];
        if (last.All(char.IsAsciiDigit)
            || labels.Any(label => label.StartsWith("0x", StringComparison.Ordinal) && label.Length > 2 && label[2..].All(char.IsAsciiHexDigit))
            || System.Net.IPAddress.TryParse(host, out _))
        {
            return false;
        }

        return host != "localhost" && !InternalSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.Ordinal));
    }

    /// <summary>
    /// The form used to recognise the same article: lower-case scheme and host, no default port, no fragment, and no tracking
    /// parameters (utm_*, click ids). Path and the remaining query are kept exactly.
    /// </summary>
    public static string Canonicalize(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var query = uri.Query.TrimStart('?');
        var kept = query.Length == 0
            ? []
            : query.Split('&', StringSplitOptions.RemoveEmptyEntries).Where(pair => !IsTracking(pair.Split('=', 2)[0])).ToList();
        var builder = new StringBuilder()
            .Append(uri.Scheme.ToLowerInvariant()).Append("://")
            .Append(uri.IdnHost.TrimEnd('.').ToLowerInvariant())
            .Append(uri.AbsolutePath);
        if (kept.Count > 0)
        {
            builder.Append('?').Append(string.Join('&', kept));
        }

        var canonical = builder.ToString();
        return canonical.Length > MaxLength ? canonical[..MaxLength] : canonical;
    }

    /// <summary>SHA-256 (lower-case hex) of the canonical URL; the unique key that prevents duplicate items.</summary>
    public static string Hash(string canonicalUrl)
    {
        ArgumentNullException.ThrowIfNull(canonicalUrl);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalUrl)));
    }

    private static bool IsTracking(string name)
    {
        var decoded = Uri.UnescapeDataString(name);
        return decoded.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) || TrackingParameters.Contains(decoded);
    }

    private static Error Invalid(string message) => Error.Validation("News.InvalidUrl", message);
}
