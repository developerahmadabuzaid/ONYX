using Onyx.Contracts.Search;

namespace Onyx.Web.ApiClients;

public interface ISearchApiClient
{
    Task<ApiResult<SiteSearchResponse>> SearchAsync(string languageCode, string query, CancellationToken cancellationToken);
}
