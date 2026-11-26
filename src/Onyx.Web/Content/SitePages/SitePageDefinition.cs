namespace Onyx.Web.Content.SitePages;

/// <summary>
/// A code-owned public page (services, company and legal pages): structure and per-language copy, loaded from
/// <c>SiteContent/*.json</c>. These pages own exact localized paths; every other path is still resolved by the CMS.
/// </summary>
public sealed record SitePageDefinition
{
    /// <summary>Stable key, e.g. <c>services.ai</c>; used for links between pages.</summary>
    public required string Key { get; init; }

    /// <summary>Parent page key for breadcrumbs (the homepage is always the root).</summary>
    public string? Parent { get; init; }

    /// <summary><c>service-index</c>, <c>service</c>, <c>page</c> or <c>legal</c>; drives structured data and layout details.</summary>
    public required string Type { get; init; }

    public string? Icon { get; init; }

    /// <summary>Start-project pillar preselected by the page's call to action (one of the wizard's pillar options).</summary>
    public string? Pillar { get; init; }

    /// <summary>Secondary offering (shown apart from the four primary engineering pillars).</summary>
    public bool Secondary { get; init; }

    /// <summary>Unapproved wording (e.g. legal drafts): rendered with a draft notice, never indexed, left out of the sitemap.</summary>
    public bool Draft { get; init; }

    /// <summary>Related page keys, shown at the end of the page.</summary>
    public IReadOnlyList<string> Related { get; init; } = [];

    public required IReadOnlyDictionary<string, SitePageText> Translations { get; init; }
}
