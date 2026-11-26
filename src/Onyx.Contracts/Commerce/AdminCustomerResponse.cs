namespace Onyx.Contracts.Commerce;

public sealed record AdminCustomerResponse(Guid Id, string Email, string DisplayName, string? Company, string Language, string Status, bool EmailVerified, bool MarketingConsent, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastSignInAtUtc, IReadOnlyList<AdminSubscriptionResponse> Subscriptions, IReadOnlyList<AdminInvoiceListItemResponse> Invoices, uint Version);
