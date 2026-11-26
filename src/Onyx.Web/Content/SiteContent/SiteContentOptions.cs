namespace Onyx.Web.Content.SiteContent;

/// <summary>How Onyx.Web caches published site content (configuration section <c>SiteContent</c>).</summary>
public sealed class SiteContentOptions
{
    public const string SectionName = "SiteContent";

    /// <summary>How long a loaded snapshot is served before a background refresh (publishing from this instance refreshes at once).</summary>
    public int CacheSeconds { get; set; } = 60;

    /// <summary>On the very first request only, how long to wait for the first load before rendering the code-owned fallback.</summary>
    public int InitialWaitMilliseconds { get; set; } = 1500;
}
