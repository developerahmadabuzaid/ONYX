namespace Onyx.Web.Content.SitePages;

/// <summary>
/// A typed page section (no free-form page builder). Kinds: <c>prose</c>, <c>offerings</c>, <c>steps</c>, <c>compare</c>,
/// <c>stack</c>, <c>pages</c>, <c>checklist</c> and <c>callout</c>.
/// </summary>
public sealed record SitePageSection
{
    public required string Kind { get; init; }

    /// <summary>Anchor id (unique within the page); sections with an id and a heading appear in the page index.</summary>
    public string? Id { get; init; }

    public string? Eyebrow { get; init; }

    public string? Heading { get; init; }

    public string? Lead { get; init; }

    public IReadOnlyList<string> Paragraphs { get; init; } = [];

    public IReadOnlyList<SitePageItem> Items { get; init; } = [];

    public string? Note { get; init; }
}
