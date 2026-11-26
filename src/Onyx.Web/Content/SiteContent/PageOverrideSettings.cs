namespace Onyx.Web.Content.SiteContent;

/// <summary>Presentation settings for a site page. Media ids end in "MediaId" so the API validates and tracks them.</summary>
public sealed record PageOverrideSettings
{
    /// <summary>Section keys not rendered (content stays; it is only hidden).</summary>
    public List<string> HiddenSections { get; init; } = [];

    /// <summary>Preferred order of section keys; sections not listed keep their code-owned order after the listed ones.</summary>
    public List<string> SectionOrder { get; init; } = [];

    public string? HeroImageMediaId { get; init; }

    public string? SocialImageMediaId { get; init; }

    public bool NoIndex { get; init; }
}
