using Onyx.Contracts.Products;

namespace Onyx.Web.ApiClients;

public sealed class ProductsApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IProductsApiClient
{
    public Task<ApiResult<IReadOnlyList<PublicProductListItemResponse>>> ListAsync(string language, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<PublicProductListItemResponse>>($"api/v1/products?lang={Uri.EscapeDataString(language)}", cancellationToken);

    public Task<ApiResult<PublicProductResponse>> GetAsync(string language, string slug, CancellationToken cancellationToken) =>
        GetAsync<PublicProductResponse>($"api/v1/products/{Uri.EscapeDataString(slug)}?lang={Uri.EscapeDataString(language)}", cancellationToken);
}
