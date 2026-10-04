using System.Text;

namespace Onyx.Web.Localization;

/// <summary>Turns the API's unencoded Unicode site paths into root-relative, UTF-8 percent-encoded paths for links and headers.</summary>
public static class SitePaths
{
    /// <summary><c>/ar/الخدمات/x</c> → <c>/ar/%D8%A7.../x</c>. A trailing slash is kept (the home page is <c>/ar/</c>).</summary>
    public static string Encode(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Normalize(NormalizationForm.FormC).Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString);
        var encoded = "/" + string.Join('/', segments);
        return path.EndsWith('/') && encoded.Length > 1 ? encoded + "/" : encoded;
    }

    /// <summary>Absolute URL for canonical, hreflang, Open Graph and sitemap use.</summary>
    public static string Absolute(string baseUrl, string encodedPath) => (baseUrl ?? string.Empty).TrimEnd('/') + encodedPath;
}
