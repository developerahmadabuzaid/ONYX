namespace Onyx.Web.Content.SiteContent;

public sealed record CopyOverrideSettings
{
    public List<string> HiddenSections { get; init; } = [];

    public List<string> SectionOrder { get; init; } = [];

    public string? WhyImageMediaId { get; init; }

    public string? SocialImageMediaId { get; init; }
}
