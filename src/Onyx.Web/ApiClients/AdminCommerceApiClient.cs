using System.Globalization;
using Onyx.Contracts.Commerce;
using Onyx.Contracts.Common;
using Onyx.Contracts.Products;

namespace Onyx.Web.ApiClients;

public sealed class AdminCommerceApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IAdminCommerceApiClient
{
    private const string Products = "api/v1/admin/products";
    private const string Customers = "api/v1/admin/customers";
    private const string Subscriptions = "api/v1/admin/subscriptions";
    private const string Invoices = "api/v1/admin/invoices";

    public Task<ApiResult<IReadOnlyList<AdminProductListItemResponse>>> ProductsAsync(string language, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<AdminProductListItemResponse>>($"{Products}?lang={Uri.EscapeDataString(language)}", cancellationToken);

    public Task<ApiResult<AdminProductResponse>> ProductAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<AdminProductResponse>($"{Products}/{id:D}", cancellationToken);

    public Task<ApiResult<AdminProductResponse>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken) =>
        PostAsync<AdminProductResponse>(Products, request, cancellationToken);

    public Task<ApiResult<AdminProductResponse>> SaveProductAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken) =>
        PutAsync<AdminProductResponse>($"{Products}/{id:D}", request, cancellationToken);

    public Task<ApiResult<AdminProductResponse>> AddPlanAsync(Guid id, SavePlanRequest request, CancellationToken cancellationToken) =>
        PostAsync<AdminProductResponse>($"{Products}/{id:D}/plans", request, cancellationToken);

    public Task<ApiResult<AdminProductResponse>> UpdatePlanAsync(Guid id, Guid planId, SavePlanRequest request, CancellationToken cancellationToken) =>
        PutAsync<AdminProductResponse>($"{Products}/{id:D}/plans/{planId:D}", request, cancellationToken);

    public Task<ApiResult<AdminProductResponse>> VerifyProductAsync(Guid id, VerifyProductRequest request, CancellationToken cancellationToken) =>
        PostAsync<AdminProductResponse>($"{Products}/{id:D}/verify", request, cancellationToken);

    public Task<ApiResult<AdminProductResponse>> PublishProductAsync(Guid id, bool publish, uint version, CancellationToken cancellationToken) =>
        PostAsync<AdminProductResponse>($"{Products}/{id:D}/{(publish ? "publish" : "unpublish")}", new ProductVersionRequest(version), cancellationToken);

    public Task<ApiResult<PagedResult<AdminCustomerListItemResponse>>> CustomersAsync(string? search, string? status, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<AdminCustomerListItemResponse>>(
            $"{Customers}?search={Uri.EscapeDataString(search ?? string.Empty)}&status={Uri.EscapeDataString(status ?? string.Empty)}&page={page.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken);

    public Task<ApiResult<AdminCustomerResponse>> CustomerAsync(Guid id, string language, CancellationToken cancellationToken) =>
        GetAsync<AdminCustomerResponse>($"{Customers}/{id:D}?lang={Uri.EscapeDataString(language)}", cancellationToken);

    public Task<ApiResult<AdminCustomerResponse>> SetCustomerStatusAsync(Guid id, CustomerStatusRequest request, string language, CancellationToken cancellationToken) =>
        PostAsync<AdminCustomerResponse>($"{Customers}/{id:D}/status?lang={Uri.EscapeDataString(language)}", request, cancellationToken);

    public Task<ApiResult<PagedResult<AdminSubscriptionResponse>>> SubscriptionsAsync(string? status, string language, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<AdminSubscriptionResponse>>(
            $"{Subscriptions}?status={Uri.EscapeDataString(status ?? string.Empty)}&lang={Uri.EscapeDataString(language)}&page={page.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken);

    public Task<ApiResult<AdminSubscriptionResponse>> DecideSubscriptionAsync(Guid id, SubscriptionDecisionRequest request, string language, CancellationToken cancellationToken) =>
        PostAsync<AdminSubscriptionResponse>($"{Subscriptions}/{id:D}/decision?lang={Uri.EscapeDataString(language)}", request, cancellationToken);

    public Task<ApiResult<PagedResult<AdminInvoiceListItemResponse>>> InvoicesAsync(string? status, Guid? customerId, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<AdminInvoiceListItemResponse>>(
            $"{Invoices}?status={Uri.EscapeDataString(status ?? string.Empty)}{(customerId is { } id ? $"&customerId={id:D}" : string.Empty)}&page={page.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken);

    public Task<ApiResult<AdminInvoiceResponse>> InvoiceAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<AdminInvoiceResponse>($"{Invoices}/{id:D}", cancellationToken);

    public Task<ApiResult<AdminInvoiceResponse>> CreateInvoiceAsync(SaveInvoiceRequest request, CancellationToken cancellationToken) =>
        PostAsync<AdminInvoiceResponse>(Invoices, request, cancellationToken);

    public Task<ApiResult<AdminInvoiceResponse>> UpdateInvoiceAsync(Guid id, SaveInvoiceRequest request, CancellationToken cancellationToken) =>
        PutAsync<AdminInvoiceResponse>($"{Invoices}/{id:D}", request, cancellationToken);

    public Task<ApiResult<AdminInvoiceResponse>> InvoiceCommandAsync(Guid id, string command, uint version, CancellationToken cancellationToken) =>
        PostAsync<AdminInvoiceResponse>($"{Invoices}/{id:D}/{Uri.EscapeDataString(command)}", new InvoiceCommandRequest(version), cancellationToken);
}
