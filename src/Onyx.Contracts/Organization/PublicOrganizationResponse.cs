namespace Onyx.Contracts.Organization;

/// <summary>
/// Owner-verified organization facts for structured data. Returned only when every required fact is verified; there are no
/// optional guesses: <see cref="Email"/> is the Brand Kit company email when one was entered. Addresses and profiles are not
/// included until they have a verified source.
/// </summary>
public sealed record PublicOrganizationResponse(string Name, string Url, Guid LogoMediaId, int? LogoWidth, int? LogoHeight, string? Email);
