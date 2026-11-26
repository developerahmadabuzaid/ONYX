namespace Onyx.Web.Content.SitePages;

/// <summary>One language of a <see cref="SitePageDefinition"/>.</summary>
public sealed record SitePageText
{
    /// <summary>Localized path without the language prefix, unencoded (e.g. <c>services/ai-automation</c>).</summary>
    public required string Path { get; init; }

    /// <summary>Short label for navigation and breadcrumbs.</summary>
    public required string Nav { get; init; }

    /// <summary>SEO title (the layout appends the site name).</summary>
    public required string Title { get; init; }

    public required string Description { get; init; }

    public string? Eyebrow { get; init; }

    public required string H1 { get; init; }

    public string? Lead { get; init; }

    /// <summary>One-sentence summary for cards, menus and related-page lists.</summary>
    public string? Summary { get; init; }

    public IReadOnlyList<SitePageSection> Sections { get; init; } = [];

    public string? CtaTitle { get; init; }

    public string? CtaText { get; init; }
}
