using System.Text;

namespace Onyx.Application.Analytics;

/// <summary>
/// Provider page values → Onyx site paths: absolute URLs reduced to their path, query strings and fragments dropped, percent-
/// encoding decoded (Arabic paths), NFC, no trailing slash except the language home (<c>/ar/</c>). The language is the first
/// segment, so Arabic and English pages are never merged.
/// </summary>
public static class AnalyticsPaths
{
    public static (string Path, string LanguageCode) Normalize(string? value)
    {
        var raw = value?.Trim() ?? string.Empty;
        if (Uri.TryCreate(raw, UriKind.Absolute, out var absolute) && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp))
        {
            raw = absolute.AbsolutePath;
        }

        raw = raw.Split('?', '#')[0];
        if (raw.Length == 0 || raw[0] != '/')
        {
            return (string.Empty, string.Empty);
        }

        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(raw);
        }
        catch (UriFormatException)
        {
            decoded = raw;
        }

        var path = decoded.Normalize(NormalizationForm.FormC);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var language = segments.Length > 0 && segments[0] is "ar" or "en" ? segments[0] : string.Empty;
        if (segments.Length == 0)
        {
            return ("/", string.Empty);
        }

        return segments.Length == 1 && language.Length > 0 ? ($"/{language}/", language) : ("/" + string.Join('/', segments), language);
    }
}
