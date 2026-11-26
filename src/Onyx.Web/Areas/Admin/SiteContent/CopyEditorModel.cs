using Onyx.Contracts.Media;
using Onyx.Contracts.SiteContent;
using Onyx.Web.Content.SiteContent;

namespace Onyx.Web.Areas.Admin.SiteContent;

/// <summary>The homepage or navigation &amp; footer editor: editable resource keys with their defaults and the current override.</summary>
public sealed record CopyEditorModel(
    string Key,
    IReadOnlyList<CopyGroup> Groups,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Defaults,
    CopyOverride Current,
    SiteContentDocumentResponse Document,
    IReadOnlyList<MediaAssetResponse> Images,
    IReadOnlyList<SiteContentRevisionResponse> Revisions,
    IReadOnlyList<string> Languages)
{
    public bool IsHome => Key == SiteContentKeys.Home;

    public string Value(string language, string key)
    {
        var value = Current.Translations.GetValueOrDefault(language)?.GetValueOrDefault(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = Defaults.GetValueOrDefault(language)?.GetValueOrDefault(key) ?? string.Empty;
        }

        return SiteCopyCatalog.IsList(key) ? string.Join('\n', value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : value;
    }

    public bool IsChanged(string language, string key) => !string.IsNullOrWhiteSpace(Current.Translations.GetValueOrDefault(language)?.GetValueOrDefault(key));
}
