using Onyx.Contracts.SiteContent;

namespace Onyx.Web.ApiClients;

public sealed class PublicSiteContentApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IPublicSiteContentApiClient
{
    public Task<ApiResult<PublicSiteContentResponse>> GetPublishedAsync(CancellationToken cancellationToken) =>
        GetAsync<PublicSiteContentResponse>("api/v1/site-content", cancellationToken);
}
