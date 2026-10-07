using Onyx.Contracts.News;
using Onyx.Web.Localization;

namespace Onyx.Web.News;

public static class NewsPages
{
    public const string Hub = "news.hub";

    /// <summary>Highest page number served (deeper pages are not linked anywhere).</summary>
    public const int MaxPage = 1_000;

    public static LocalizedRoute Route => new(Hub, "NewsHub", "Index", NewsRouteSegments.Hub);

    /// <summary>Only absolute http(s) links are ever rendered as the "original article" link.</summary>
    public static bool IsSafeExternalLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && string.IsNullOrEmpty(uri.UserInfo);
}
