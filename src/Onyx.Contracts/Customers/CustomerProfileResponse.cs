namespace Onyx.Contracts.Customers;

public sealed record CustomerProfileResponse(Guid Id, string Email, string DisplayName, string? Company, string Language, bool MarketingConsent, string TermsVersion, DateTimeOffset TermsAcceptedAtUtc, DateTimeOffset CreatedAtUtc, uint Version);
