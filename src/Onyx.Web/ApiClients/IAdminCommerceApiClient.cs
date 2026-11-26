using Onyx.Contracts.Commerce;
using Onyx.Contracts.Common;
using Onyx.Contracts.Products;

namespace Onyx.Web.ApiClients;

/// <summary>Admin commerce: product catalogue, customers, subscriptions and invoices.</summary>
public interface IAdminCommerceApiClient
{
    Task<ApiResult<IReadOnlyList<AdminProductListItemResponse>>> ProductsAsync(string language, CancellationToken cancellationToken);

    Task<ApiResult<AdminProductResponse>> ProductAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<AdminProductResponse>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken);

    Task<ApiResult<AdminProductResponse>> SaveProductAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken);

    Task<ApiResult<AdminProductResponse>> AddPlanAsync(Guid id, SavePlanRequest request, CancellationToken cancellationToken);

    Task<ApiResult<AdminProductResponse>> UpdatePlanAsync(Guid id, Guid planId, SavePlanRequest request, CancellationToken cancellationToken);

    Task<ApiResult<AdminProductResponse>> VerifyProductAsync(Guid id, VerifyProductRequest request, CancellationToken cancellationToken);

    Task<ApiResult<AdminProductResponse>> PublishProductAsync(Guid id, bool publish, uint version, CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<AdminCustomerListItemResponse>>> CustomersAsync(string? search, string? status, int page, CancellationToken cancellationToken);

    Task<ApiResult<AdminCustomerResponse>> CustomerAsync(Guid id, string language, CancellationToken cancellationToken);

    Task<ApiResult<AdminCustomerResponse>> SetCustomerStatusAsync(Guid id, CustomerStatusRequest request, string language, CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<AdminSubscriptionResponse>>> SubscriptionsAsync(string? status, string language, int page, CancellationToken cancellationToken);

    Task<ApiResult<AdminSubscriptionResponse>> DecideSubscriptionAsync(Guid id, SubscriptionDecisionRequest request, string language, CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<AdminInvoiceListItemResponse>>> InvoicesAsync(string? status, Guid? customerId, int page, CancellationToken cancellationToken);

    Task<ApiResult<AdminInvoiceResponse>> InvoiceAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<AdminInvoiceResponse>> CreateInvoiceAsync(SaveInvoiceRequest request, CancellationToken cancellationToken);

    Task<ApiResult<AdminInvoiceResponse>> UpdateInvoiceAsync(Guid id, SaveInvoiceRequest request, CancellationToken cancellationToken);

    Task<ApiResult<AdminInvoiceResponse>> InvoiceCommandAsync(Guid id, string command, uint version, CancellationToken cancellationToken);
}
