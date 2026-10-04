namespace Onyx.Infrastructure.Seo;

/// <summary>
/// Bound from <c>Site</c> in Onyx.API and Onyx.Worker. Must carry the same values as Onyx.Web's <c>Site</c> section: the public
/// origin used for absolute URLs, and the launch switch (nothing is announced to search engines before launch).
/// </summary>
public sealed class PublicSiteOptions
{
    public const string SectionName = "Site";

    public string PublicBaseUrl { get; set; } = string.Empty;

    public bool AllowIndexing { get; set; }

    /// <summary>The origin (scheme, host, port) when <see cref="PublicBaseUrl"/> is an absolute https URL; otherwise null.</summary>
    public Uri? Origin =>
        Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query)
            ? new Uri(uri.GetLeftPart(UriPartial.Authority))
            : null;
}
