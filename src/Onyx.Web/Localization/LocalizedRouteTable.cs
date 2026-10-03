using System.Collections.Concurrent;
using System.Text;

namespace Onyx.Web.Localization;

/// <summary>
/// Per-language route segments for public pages (docs/architecture.md §22). Inbound requests are resolved by
/// <see cref="LocalizedRouteTransformer"/>; outbound URLs are built here, percent-encoded as UTF-8.
/// Pages register their segments as they are built (Stage 6 onwards).
/// </summary>
public sealed class LocalizedRouteTable
{
    private readonly ConcurrentDictionary<string, LocalizedRoute> _byKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Language, string Segment), LocalizedRoute> _bySegment = new();

    public void Register(LocalizedRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        foreach (var (language, segment) in route.Segments)
        {
            var normalized = Normalize(segment);
            if (_bySegment.TryGetValue((language, normalized), out var existing) && existing.Key != route.Key)
            {
                throw new InvalidOperationException($"Segment '{segment}' ({language}) is already used by '{existing.Key}'.");
            }

            _bySegment[(language, normalized)] = route;
        }

        _byKey[route.Key] = route;
    }

    public bool TryMatch(string language, string segment, out LocalizedRoute route) =>
        _bySegment.TryGetValue((language, Normalize(segment)), out route!);

    /// <summary>Root-relative, percent-encoded path, e.g. <c>/ar/%D8%A7%D9%84%D8%AE%D8%AF%D9%85%D8%A7%D8%AA/slug</c>.</summary>
    public string? PathFor(string key, string language, params string[] slugs)
    {
        if (!_byKey.TryGetValue(key, out var route) || !route.Segments.TryGetValue(language, out var segment))
        {
            return null;
        }

        var parts = new[] { language, segment }.Concat(slugs).Where(part => !string.IsNullOrEmpty(part)).Select(part => Uri.EscapeDataString(Normalize(part)));
        return "/" + string.Join('/', parts);
    }

    private static string Normalize(string value) => value.Normalize(NormalizationForm.FormC);
}
