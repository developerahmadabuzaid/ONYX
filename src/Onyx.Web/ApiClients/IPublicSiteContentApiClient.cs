using Onyx.Contracts.SiteContent;

namespace Onyx.Web.ApiClients;

public interface IPublicSiteContentApiClient
{
    /// <summary>Every published site content override (cached by <c>SiteContentProvider</c>).</summary>
    Task<ApiResult<PublicSiteContentResponse>> GetPublishedAsync(CancellationToken cancellationToken);
}
