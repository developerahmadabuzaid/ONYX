using System.Text.Json;
using Onyx.Contracts.SiteContent;

namespace Onyx.Web.Content.SiteContent;

/// <summary>
/// The published site content overrides, parsed. Parsing is tolerant: a malformed document is skipped (its page simply shows the
/// code-owned copy) instead of breaking the site.
/// </summary>
public sealed record SiteContentSnapshot(
    IReadOnlyDictionary<string, PageOverride> Pages,
    CopyOverride? Home,
    CopyOverride? Shared,
    SiteSettingsContent? Settings)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public static SiteContentSnapshot Empty { get; } = new(new Dictionary<string, PageOverride>(StringComparer.Ordinal), null, null, null);

    public static SiteContentSnapshot From(IReadOnlyDictionary<string, string> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var snapshot = Empty;
        foreach (var (key, json) in documents)
        {
            snapshot = snapshot.With(key, json);
        }

        return snapshot;
    }

    /// <summary>A copy with one document replaced (used for publishing updates and for admin previews of a draft).</summary>
    public SiteContentSnapshot With(string key, string? json)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.StartsWith(SiteContentKeys.PagePrefix, StringComparison.Ordinal))
        {
            var pages = new Dictionary<string, PageOverride>(Pages, StringComparer.Ordinal);
            var pageKey = key[SiteContentKeys.PagePrefix.Length..];
            if (TryParse<PageOverride>(json) is { } page)
            {
                pages[pageKey] = page;
            }
            else
            {
                pages.Remove(pageKey);
            }

            return this with { Pages = pages };
        }

        return key switch
        {
            SiteContentKeys.Home => this with { Home = TryParse<CopyOverride>(json) },
            SiteContentKeys.Shared => this with { Shared = TryParse<CopyOverride>(json) },
            SiteContentKeys.Settings => this with { Settings = TryParse<SiteSettingsContent>(json) },
            _ => this,
        };
    }

    public static T? TryParse<T>(string? json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
