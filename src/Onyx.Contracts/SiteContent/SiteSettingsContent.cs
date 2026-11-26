namespace Onyx.Contracts.SiteContent;

/// <summary>
/// The website settings document: verified contact details only. Every field is optional and stays empty until the owner enters a
/// real value; localized fields are language code → text.
/// </summary>
public sealed record SiteSettingsContent
{
    public IReadOnlyDictionary<string, string> CompanyName { get; init; } = new Dictionary<string, string>();

    public string? ContactEmail { get; init; }

    public IReadOnlyList<SitePhone> Phones { get; init; } = [];

    public IReadOnlyList<SiteOffice> Offices { get; init; } = [];

    public IReadOnlyList<SiteSocialLink> SocialLinks { get; init; } = [];
}
