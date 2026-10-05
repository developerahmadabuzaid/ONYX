namespace Onyx.Contracts.Leads;

/// <summary>One marketing touch: landing path, referring origin and campaign (UTM) tags.</summary>
public sealed record TouchPointDto(
    string? LandingPath,
    string? Referrer,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmContent,
    string? UtmTerm,
    DateTimeOffset? AtUtc);
