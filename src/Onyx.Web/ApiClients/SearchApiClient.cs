using Onyx.Contracts.Search;

namespace Onyx.Web.ApiClients;

public sealed class SearchApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), ISearchApiClient
{
    public Task<ApiResult<SiteSearchResponse>> SearchAsync(string languageCode, string query, CancellationToken cancellationToken) =>
        GetAsync<SiteSearchResponse>($"api/v1/search?language={Uri.EscapeDataString(languageCode)}&q={Uri.EscapeDataString(query)}", cancellationToken);
}
