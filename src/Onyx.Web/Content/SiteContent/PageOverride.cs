namespace Onyx.Web.Content.SiteContent;

/// <summary>Editor overrides for one site page (document <c>page:{key}</c>): per-language text plus presentation settings.</summary>
public sealed record PageOverride
{
    public Dictionary<string, PageTextOverride> Translations { get; init; } = new(StringComparer.Ordinal);

    public PageOverrideSettings Settings { get; init; } = new();
}
