namespace Onyx.Contracts.Customers;

public sealed record UpdateCustomerProfileRequest(string DisplayName, string? Company, string Language, bool MarketingConsent, uint Version);
