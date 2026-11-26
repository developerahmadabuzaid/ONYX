using Onyx.Application.Commerce;
using Onyx.Contracts.Products;
using Onyx.Domain.Common;

namespace Onyx.Application.Products;

/// <summary>Published products for visitors (no account needed). Prices appear only where an administrator made them public.</summary>
public sealed class ProductCatalogService(ICommerceReadModel readModel)
{
    public Task<IReadOnlyList<PublicProductListItemResponse>> ListAsync(string language, CancellationToken cancellationToken) =>
        readModel.PublicProductsAsync(CommerceErrors.Language(language), cancellationToken);

    public async Task<Result<PublicProductResponse>> GetAsync(string language, string slug, CancellationToken cancellationToken) =>
        await readModel.PublicProductAsync(CommerceErrors.Language(language), slug ?? string.Empty, cancellationToken).ConfigureAwait(false) is { } product
            ? product
            : CommerceErrors.ProductNotFound;
}
