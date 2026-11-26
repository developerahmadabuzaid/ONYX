using Onyx.Contracts.Customers;

namespace Onyx.Web.ApiClients;

/// <summary>The signed-in customer's own account; the session token is attached by <see cref="Customers.CustomerSessionHandler"/>.</summary>
public interface ICustomerApiClient
{
    Task<ApiResult<CustomerOverviewResponse>> OverviewAsync(string language, CancellationToken cancellationToken);

    Task<ApiResult<CustomerProfileResponse>> ProfileAsync(CancellationToken cancellationToken);

    Task<ApiResult<CustomerProfileResponse>> UpdateProfileAsync(UpdateCustomerProfileRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> ChangePasswordAsync(CustomerChangePasswordRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteAccountAsync(DeleteCustomerAccountRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> SignOutAsync(CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<CustomerSubscriptionResponse>>> SubscriptionsAsync(string language, CancellationToken cancellationToken);

    Task<ApiResult<CustomerSubscriptionResponse>> StartSubscriptionAsync(StartSubscriptionRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> CancelSubscriptionAsync(Guid id, uint version, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<CustomerInvoiceResponse>>> InvoicesAsync(CancellationToken cancellationToken);

    Task<ApiResult<CustomerInvoiceResponse>> InvoiceAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<AiDemoRunResponse>> StartDemoAsync(StartAiDemoRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<AiDemoRunResponse>>> DemoRunsAsync(string productSlug, string language, CancellationToken cancellationToken);
}
