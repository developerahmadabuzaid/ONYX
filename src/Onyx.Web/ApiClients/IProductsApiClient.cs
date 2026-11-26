using Onyx.Contracts.Products;

namespace Onyx.Web.ApiClients;

public interface IProductsApiClient
{
    Task<ApiResult<IReadOnlyList<PublicProductListItemResponse>>> ListAsync(string language, CancellationToken cancellationToken);

    Task<ApiResult<PublicProductResponse>> GetAsync(string language, string slug, CancellationToken cancellationToken);
}
