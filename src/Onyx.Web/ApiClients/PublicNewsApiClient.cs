using System.Globalization;
using Onyx.Contracts.Content;
using Onyx.Contracts.News;

namespace Onyx.Web.ApiClients;

public sealed class PublicNewsApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IPublicNewsApiClient
{
    public Task<ApiResult<PublicNewsListResponse>> GetHubAsync(string languageCode, int page, CancellationToken cancellationToken) =>
        GetAsync<PublicNewsListResponse>($"api/v1/news?language={Uri.EscapeDataString(languageCode)}&page={page.ToString(CultureInfo.InvariantCulture)}", cancellationToken);

    public Task<ApiResult<NewsResolveResponse>> ResolveAsync(string languageCode, string slug, int page, CancellationToken cancellationToken) =>
        GetAsync<NewsResolveResponse>(
            $"api/v1/news/resolve?language={Uri.EscapeDataString(languageCode)}&slug={Uri.EscapeDataString(slug)}&page={page.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken);

    public async Task<IReadOnlyList<PublicNewsItemSummaryResponse>> GetLatestAsync(string languageCode, CancellationToken cancellationToken)
    {
        var result = await GetAsync<IReadOnlyList<PublicNewsItemSummaryResponse>>($"api/v1/news/latest?language={Uri.EscapeDataString(languageCode)}", cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess && result.Value is not null ? result.Value : [];
    }

    public Task<ApiResult<IReadOnlyList<SitemapEntryResponse>>> GetSitemapAsync(string languageCode, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<SitemapEntryResponse>>($"api/v1/news/sitemap?language={Uri.EscapeDataString(languageCode)}", cancellationToken);
}
