using Onyx.Contracts.Media;
using Onyx.Contracts.SiteContent;
using Onyx.Web.Content.SiteContent;
using Onyx.Web.Content.SitePages;

namespace Onyx.Web.Areas.Admin.SiteContent;

/// <summary>The site page editor: the code-owned page (structure and defaults) and the current override (draft, else live).</summary>
public sealed record PageEditorModel(
    SitePageDefinition Page,
    PageOverride Current,
    SiteContentDocumentResponse Document,
    IReadOnlyList<MediaAssetResponse> Images,
    IReadOnlyList<SiteContentRevisionResponse> Revisions,
    IReadOnlyList<string> Languages,
    IReadOnlyDictionary<string, string> PublicPaths)
{
    public string Key => SiteContentKeys.Page(Page.Key);

    public PageTextOverride? TextOverride(string language) => Current.Translations.GetValueOrDefault(language);

    public SectionOverride? SectionOverride(string language, string sectionKey) => TextOverride(language)?.Sections.GetValueOrDefault(sectionKey);

    public ItemOverride? ItemOverride(string language, string sectionKey, string itemKey) => SectionOverride(language, sectionKey)?.Items.GetValueOrDefault(itemKey);

    /// <summary>The value shown in a field: the editor's override, else the code-owned text.</summary>
    public static string Show(string? overrideValue, string? fallback) => string.IsNullOrWhiteSpace(overrideValue) ? fallback ?? string.Empty : overrideValue;

    public static string ShowList(IReadOnlyList<string>? overrideValue, IReadOnlyList<string> fallback, bool paragraphs = false) =>
        SiteContentForms.JoinLines(overrideValue is { Count: > 0 } ? overrideValue : fallback, paragraphs);
}
