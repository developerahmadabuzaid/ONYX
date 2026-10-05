namespace Onyx.Contracts.Leads;

public sealed record ConsentResponse(
    Guid Id,
    bool ContactConsent,
    bool MarketingConsent,
    bool MarketingActive,
    string ConsentVersion,
    DateTimeOffset GivenAtUtc,
    DateTimeOffset? MarketingWithdrawnAtUtc);
