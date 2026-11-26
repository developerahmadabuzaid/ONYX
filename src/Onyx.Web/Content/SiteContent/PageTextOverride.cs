namespace Onyx.Web.Content.SiteContent;

/// <summary>Overridable page text in one language; null or blank values fall back to the code-owned copy.</summary>
public sealed record PageTextOverride
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? OgTitle { get; init; }

    public string? OgDescription { get; init; }

    public string? Nav { get; init; }

    public string? Summary { get; init; }

    public string? Eyebrow { get; init; }

    public string? H1 { get; init; }

    public string? Lead { get; init; }

    public string? CtaTitle { get; init; }

    public string? CtaText { get; init; }

    /// <summary>Keyed by section id (or <c>#index</c> for sections without one).</summary>
    public Dictionary<string, SectionOverride> Sections { get; init; } = new(StringComparer.Ordinal);
}
