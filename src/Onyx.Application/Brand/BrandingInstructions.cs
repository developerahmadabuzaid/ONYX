namespace Onyx.Application.Brand;

/// <summary>Branding to apply to a generated image: only real Brand Kit values (the official logo asset, URL, email, colours).</summary>
public sealed record BrandingInstructions(Guid OfficialLogoMediaId, string? WebsiteUrl, string? CompanyEmail, IReadOnlyList<string> ColorHexes, string? Notes);
