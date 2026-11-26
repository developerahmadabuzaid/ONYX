using Onyx.Contracts.Customers;

namespace Onyx.Web.ApiClients;

public sealed class CustomerApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), ICustomerApiClient
{
    private const string Base = "api/v1/customer";

    public Task<ApiResult<CustomerOverviewResponse>> OverviewAsync(string language, CancellationToken cancellationToken) =>
        GetAsync<CustomerOverviewResponse>($"{Base}/overview?lang={Uri.EscapeDataString(language)}", cancellationToken);

    public Task<ApiResult<CustomerProfileResponse>> ProfileAsync(CancellationToken cancellationToken) =>
        GetAsync<CustomerProfileResponse>($"{Base}/profile", cancellationToken);

    public Task<ApiResult<CustomerProfileResponse>> UpdateProfileAsync(UpdateCustomerProfileRequest request, CancellationToken cancellationToken) =>
        PutAsync<CustomerProfileResponse>($"{Base}/profile", request, cancellationToken);

    public Task<ApiResult<NoContent>> ChangePasswordAsync(CustomerChangePasswordRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"{Base}/password", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteAccountAsync(DeleteCustomerAccountRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"{Base}/delete", request, cancellationToken);

    public Task<ApiResult<NoContent>> SignOutAsync(CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"{Base}/sign-out", null, cancellationToken);

    public Task<ApiResult<IReadOnlyList<CustomerSubscriptionResponse>>> SubscriptionsAsync(string language, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<CustomerSubscriptionResponse>>($"{Base}/subscriptions?lang={Uri.EscapeDataString(language)}", cancellationToken);

    public Task<ApiResult<CustomerSubscriptionResponse>> StartSubscriptionAsync(StartSubscriptionRequest request, CancellationToken cancellationToken) =>
        PostAsync<CustomerSubscriptionResponse>($"{Base}/subscriptions", request, cancellationToken);

    public Task<ApiResult<NoContent>> CancelSubscriptionAsync(Guid id, uint version, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"{Base}/subscriptions/{id:D}/cancel", new CancelSubscriptionRequest(version), cancellationToken);

    public Task<ApiResult<IReadOnlyList<CustomerInvoiceResponse>>> InvoicesAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<CustomerInvoiceResponse>>($"{Base}/invoices", cancellationToken);

    public Task<ApiResult<CustomerInvoiceResponse>> InvoiceAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<CustomerInvoiceResponse>($"{Base}/invoices/{id:D}", cancellationToken);

    public Task<ApiResult<AiDemoRunResponse>> StartDemoAsync(StartAiDemoRequest request, CancellationToken cancellationToken) =>
        PostAsync<AiDemoRunResponse>($"{Base}/ai-demo/runs", request, cancellationToken);

    public Task<ApiResult<IReadOnlyList<AiDemoRunResponse>>> DemoRunsAsync(string productSlug, string language, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<AiDemoRunResponse>>($"{Base}/ai-demo/products/{Uri.EscapeDataString(productSlug)}/runs?lang={Uri.EscapeDataString(language)}", cancellationToken);
}
