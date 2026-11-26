namespace Onyx.Web.Content.SiteContent;

public sealed record SectionOverride
{
    public string? Eyebrow { get; init; }

    public string? Heading { get; init; }

    public string? Lead { get; init; }

    public string? Note { get; init; }

    public List<string>? Paragraphs { get; init; }

    /// <summary>Keyed by item id (or <c>#index</c> for items without one).</summary>
    public Dictionary<string, ItemOverride> Items { get; init; } = new(StringComparer.Ordinal);
}
