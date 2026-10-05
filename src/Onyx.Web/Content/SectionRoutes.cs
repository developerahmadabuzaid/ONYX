using System.Text;
using Onyx.Contracts.Content;

namespace Onyx.Web.Content;

/// <summary>Maps a localized path (without language) to a public section listing, using the shared route segments.</summary>
public static class SectionRoutes
{
    public static string? Match(string languageCode, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var normalized = path.Normalize(NormalizationForm.FormC).Trim('/');
        return ContentRouteSegments.Sections
            .FirstOrDefault(section => section.Value.TryGetValue(languageCode, out var localized) && string.Equals(localized, normalized, StringComparison.Ordinal))
            .Key;
    }

    /// <summary>The section's site path in a language (unencoded), e.g. <c>/ar/أعمالنا</c>.</summary>
    public static string? PathOf(string section, string languageCode) =>
        ContentRouteSegments.Sections.TryGetValue(section, out var paths) && paths.TryGetValue(languageCode, out var path) ? $"/{languageCode}/{path}" : null;
}
