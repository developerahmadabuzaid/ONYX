using Onyx.Contracts.Content;
using Onyx.Contracts.News;

namespace Onyx.Web.Content;

/// <summary>Homepage sections built so far; each is hidden when it has nothing real to show.</summary>
public sealed record HomePageModel(IReadOnlyList<ListingItemResponse> FeaturedWork, IReadOnlyList<PublicNewsItemSummaryResponse> LatestNews, string? NewsHubPath);
