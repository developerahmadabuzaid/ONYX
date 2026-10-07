using Onyx.Contracts.Content;
using Onyx.Contracts.News;

namespace Onyx.Web.ApiClients;

public interface IPublicNewsApiClient
{
    Task<ApiResult<PublicNewsListResponse>> GetHubAsync(string languageCode, int page, CancellationToken cancellationToken);

    Task<ApiResult<NewsResolveResponse>> ResolveAsync(string languageCode, string slug, int page, CancellationToken cancellationToken);

    /// <summary>Homepage section: an unreachable API simply means no news block.</summary>
    Task<IReadOnlyList<PublicNewsItemSummaryResponse>> GetLatestAsync(string languageCode, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<SitemapEntryResponse>>> GetSitemapAsync(string languageCode, CancellationToken cancellationToken);
}
