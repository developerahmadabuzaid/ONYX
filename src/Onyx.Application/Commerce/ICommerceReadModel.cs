using Onyx.Contracts.Commerce;
using Onyx.Contracts.Common;
using Onyx.Contracts.Customers;
using Onyx.Contracts.Products;

namespace Onyx.Application.Commerce;

/// <summary>Read-only projections for the product catalogue, the customer portal and the Admin commerce screens.</summary>
public interface ICommerceReadModel
{
    Task<IReadOnlyList<PublicProductListItemResponse>> PublicProductsAsync(string language, CancellationToken cancellationToken);

    Task<PublicProductResponse?> PublicProductAsync(string language, string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminProductListItemResponse>> AdminProductsAsync(string language, CancellationToken cancellationToken);

    Task<AdminProductResponse?> AdminProductAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ProductSlugTakenAsync(string language, string slug, Guid exceptProductId, CancellationToken cancellationToken);

    Task<CustomerProfileResponse?> CustomerProfileAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerSubscriptionResponse>> CustomerSubscriptionsAsync(Guid customerId, string language, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerInvoiceResponse>> CustomerInvoicesAsync(Guid customerId, int max, CancellationToken cancellationToken);

    Task<CustomerInvoiceResponse?> CustomerInvoiceAsync(Guid customerId, Guid invoiceId, CancellationToken cancellationToken);

    Task<AiDemoRunResponse?> AiDemoRunAsync(Guid customerId, Guid runId, string language, CancellationToken cancellationToken);

    Task<IReadOnlyList<AiDemoRunResponse>> RecentAiDemoRunsAsync(Guid customerId, Guid productId, string language, int max, CancellationToken cancellationToken);

    Task<int> AiDemoRunsSinceAsync(Guid? customerId, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    Task<PagedResult<AdminCustomerListItemResponse>> AdminCustomersAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken);

    Task<AdminCustomerResponse?> AdminCustomerAsync(Guid id, string language, CancellationToken cancellationToken);

    Task<PagedResult<AdminSubscriptionResponse>> AdminSubscriptionsAsync(string? status, string language, int page, int pageSize, CancellationToken cancellationToken);

    Task<AdminSubscriptionResponse?> AdminSubscriptionAsync(Guid id, string language, CancellationToken cancellationToken);

    Task<PagedResult<AdminInvoiceListItemResponse>> AdminInvoicesAsync(string? status, Guid? customerId, int page, int pageSize, CancellationToken cancellationToken);

    Task<AdminInvoiceResponse?> AdminInvoiceAsync(Guid id, CancellationToken cancellationToken);
}
