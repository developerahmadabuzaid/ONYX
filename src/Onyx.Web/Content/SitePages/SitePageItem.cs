namespace Onyx.Web.Content.SitePages;

/// <summary>An item inside a section: an offering, a step, a technology group, a linked page or a checklist entry.</summary>
public sealed record SitePageItem
{
    /// <summary>Anchor id, so a specific offering can be linked directly (e.g. <c>…/ai-automation#rag</c>).</summary>
    public string? Id { get; init; }

    public required string Title { get; init; }

    public string? Text { get; init; }

    public IReadOnlyList<string> Points { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Technologies or capabilities listed only once the owner confirms them (shown with a "to be confirmed" label).</summary>
    public IReadOnlyList<string> Pending { get; init; } = [];

    public IReadOnlyList<string> Deliverables { get; init; } = [];

    public IReadOnlyList<string> Involvement { get; init; } = [];

    /// <summary>Linked page: a site page key, or a code route key such as <c>lead.contact</c>.</summary>
    public string? Page { get; init; }

    public string? Icon { get; init; }

    /// <summary><c>verify</c>: a capability the owner still has to confirm before launch (labelled on the page).</summary>
    public string? Flag { get; init; }
}
