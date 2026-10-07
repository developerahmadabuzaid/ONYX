using Onyx.Contracts.Search;
using Onyx.Web.Localization;

namespace Onyx.Web.Search;

public static class SearchPages
{
    public const string Search = "site.search";

    /// <summary>Longest query the page sends (the API caps again).</summary>
    public const int MaxQueryLength = 100;

    public static LocalizedRoute Route => new(Search, "SiteSearch", "Index", SearchRouteSegments.Search);
}
