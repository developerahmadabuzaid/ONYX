using Onyx.Web.Content.SitePages;

namespace Onyx.Web.Content.SiteContent;

/// <summary>Site pages as visitors see them in this request: the code-owned catalog with published (or previewed) overrides.</summary>
public sealed class EffectiveSitePages(SitePageCatalog catalog, SiteContentContext context)
{
    private readonly Dictionary<string, SitePageDefinition?> _cache = new(StringComparer.Ordinal);

    public SitePageCatalog Catalog => catalog;

    public SitePageDefinition? Find(string key)
    {
        if (!_cache.TryGetValue(key, out var page))
        {
            page = catalog.Find(key) is { } definition ? SitePageOverlay.Apply(definition, context.Snapshot.Pages.GetValueOrDefault(key)) : null;
            _cache[key] = page;
        }

        return page;
    }

    public PageOverrideSettings Settings(string key) => context.Snapshot.Pages.GetValueOrDefault(key)?.Settings ?? new PageOverrideSettings();

    /// <summary>Navigation label in a language (an editor override, else the code-owned label).</summary>
    public string? Label(string key, string language) => Find(key)?.Translations.GetValueOrDefault(language)?.Nav;
}
