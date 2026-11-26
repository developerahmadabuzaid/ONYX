using System.Globalization;
using Microsoft.Extensions.Localization;
using Onyx.Contracts.SiteContent;
using Onyx.Web.Resources;

namespace Onyx.Web.Content.SiteContent;

/// <summary>
/// Copy for the homepage, navigation and footer: the published override for the request language when an editor set one,
/// otherwise the resource-file text. Also exposes homepage presentation settings and the website settings.
/// </summary>
public sealed class SiteCopy(SiteContentContext context, IStringLocalizer<SharedResource> localizer)
{
    private static string Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public string this[string key]
    {
        get
        {
            var document = SiteCopyCatalog.IsHomeKey(key) ? context.Snapshot.Home : SiteCopyCatalog.IsSharedKey(key) ? context.Snapshot.Shared : null;
            if (document is not null
                && document.Translations.TryGetValue(Language, out var values)
                && values.TryGetValue(key, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            return localizer[key].Value;
        }
    }

    public CopyOverrideSettings HomeSettings => context.Snapshot.Home?.Settings ?? new CopyOverrideSettings();

    public bool IsHomeSectionVisible(string section) => !HomeSettings.HiddenSections.Contains(section, StringComparer.Ordinal);

    /// <summary>The movable homepage sections in the editor's order (unknown entries ignored, missing ones appended).</summary>
    public IReadOnlyList<string> HomeSectionOrder()
    {
        var preferred = HomeSettings.SectionOrder.Where(SiteCopyCatalog.HomeMovableSections.Contains).Distinct(StringComparer.Ordinal);
        return preferred.Concat(SiteCopyCatalog.HomeMovableSections).Distinct(StringComparer.Ordinal).ToList();
    }

    public SiteSettingsContent? Settings => context.Snapshot.Settings;

    public bool IsPreview => context.IsPreview;

    /// <summary>A localized settings value in the request language, falling back to the other language, else null.</summary>
    public static string? Localized(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null)
        {
            return null;
        }

        if (values.TryGetValue(Language, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return values.Values.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))?.Trim();
    }

    /// <summary>Image path for this request: the public media path, or the admin file route in a preview (draft images are not public yet).</summary>
    public string? ImagePath(string? mediaId) => Guid.TryParse(mediaId, out var id) ? (context.IsPreview ? $"/admin/media/{id:D}/file" : $"/media/{id:D}") : null;

    /// <summary>Public path of an image chosen in the media library (served by the existing media endpoint), or null.</summary>
    public static string? MediaPath(string? mediaId) => Guid.TryParse(mediaId, out var id) ? $"/media/{id:D}" : null;
}
