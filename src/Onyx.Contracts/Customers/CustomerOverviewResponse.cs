namespace Onyx.Contracts.Customers;

public sealed record CustomerOverviewResponse(CustomerProfileResponse Profile, IReadOnlyList<CustomerSubscriptionResponse> Subscriptions, IReadOnlyList<CustomerInvoiceResponse> RecentInvoices, CustomerUsageResponse Usage);
