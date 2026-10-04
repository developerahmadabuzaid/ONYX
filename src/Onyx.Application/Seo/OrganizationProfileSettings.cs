namespace Onyx.Application.Seo;

/// <param name="StructuredDataEnabled">Explicit owner switch, off by default: turn on only after verifying the Brand Kit logo and website.</param>
/// <param name="PublicBaseUrl">The public site origin; the Brand Kit website must be this same origin.</param>
public sealed record OrganizationProfileSettings(bool StructuredDataEnabled, string? PublicBaseUrl)
{
    public static OrganizationProfileSettings Disabled { get; } = new(false, null);
}
