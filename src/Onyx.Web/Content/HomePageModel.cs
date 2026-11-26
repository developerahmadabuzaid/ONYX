using Onyx.Contracts.Content;
using Onyx.Contracts.News;

namespace Onyx.Web.Content;

/// <summary>Homepage data sections; each is hidden when it has nothing real to show.</summary>
public sealed record HomePageModel(
    IReadOnlyList<ListingItemResponse> FeaturedWork,
    IReadOnlyList<PublicNewsItemSummaryResponse> LatestNews,
    string? NewsHubPath,
    IReadOnlyList<ListingItemResponse> LatestInsights);
