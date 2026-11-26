using System.Reflection;
using System.Text;
using System.Text.Json;
using Onyx.Web.Localization;

namespace Onyx.Web.Content.SitePages;

/// <summary>
/// The code-owned public pages, loaded once from the embedded <c>SiteContent/*.json</c> files. Paths are matched exactly
/// (NFC, without surrounding slashes), so these pages coexist with CMS content under the same first segment.
/// </summary>
public sealed class SitePageCatalog
{
    private const string ResourcePrefix = "Onyx.Web.SiteContent.";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };

    private readonly Dictionary<string, SitePageDefinition> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Language, string Path), SitePageDefinition> _byPath = new();
    private readonly List<SitePageDefinition> _pages = [];

    public SitePageCatalog(IEnumerable<SitePageDefinition> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        foreach (var page in pages)
        {
            if (!_byKey.TryAdd(page.Key, page))
            {
                throw new InvalidOperationException($"Duplicate site page key '{page.Key}'.");
            }

            foreach (var (language, text) in page.Translations)
            {
                if (!_byPath.TryAdd((language, Normalize(text.Path)), page))
                {
                    throw new InvalidOperationException($"Duplicate site page path '{text.Path}' ({language}).");
                }
            }

            _pages.Add(page);
        }

        foreach (var page in _pages.Where(page => page.Parent is not null && !_byKey.ContainsKey(page.Parent)))
        {
            throw new InvalidOperationException($"Site page '{page.Key}' has an unknown parent '{page.Parent}'.");
        }
    }

    public IReadOnlyList<SitePageDefinition> Pages => _pages;

    /// <summary>Loads every embedded <c>SiteContent/*.json</c> page definition.</summary>
    public static SitePageCatalog LoadEmbedded()
    {
        var assembly = typeof(SitePageCatalog).Assembly;
        var pages = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => Read(assembly, name));
        return new SitePageCatalog(pages);
    }

    public SitePageDefinition? Find(string key) => _byKey.GetValueOrDefault(key);

    public bool TryMatch(string language, string path, out SitePageDefinition page) =>
        _byPath.TryGetValue((language, Normalize(path)), out page!);

    /// <summary>Root-relative, percent-encoded path of a page in a language, or null when it has no such translation.</summary>
    public string? PathFor(string key, string language) => UnencodedPathFor(key, language) is { } path ? SitePaths.Encode(path) : null;

    /// <summary>Unencoded site path (<c>/ar/الخدمات</c>), as the sitemap expects.</summary>
    public string? UnencodedPathFor(string key, string language) =>
        _byKey.TryGetValue(key, out var page) && page.Translations.TryGetValue(language, out var text) ? $"/{language}/{Normalize(text.Path)}" : null;

    /// <summary>Ancestors from the top level down (excluding the page itself and the homepage).</summary>
    public IReadOnlyList<SitePageDefinition> Ancestors(SitePageDefinition page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var chain = new List<SitePageDefinition>();
        for (var parent = page.Parent; parent is not null && _byKey.TryGetValue(parent, out var ancestor); parent = ancestor.Parent)
        {
            chain.Insert(0, ancestor);
        }

        return chain;
    }

    public IReadOnlyList<SitePageDefinition> Children(string key) => _pages.Where(page => page.Parent == key).ToList();

    private static SitePageDefinition Read(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded site page '{resourceName}'.");
        return JsonSerializer.Deserialize<SitePageDefinition>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Empty site page definition '{resourceName}'.");
    }

    private static string Normalize(string path) => path.Normalize(NormalizationForm.FormC).Trim('/');
}
