using Onyx.Application.Localization;
using Onyx.Contracts.Search;
using Onyx.Domain.Common;

namespace Onyx.Application.Search;

/// <summary>
/// Public site search (architecture.md §18): validates and caps the query, normalizes it with the same normalizer used for
/// indexing, asks the engine and groups the results. Queries are never stored or logged; the response holds published, public
/// content only.
/// </summary>
public sealed class SiteSearchQueryService(ISiteSearchService engine)
{
    public const int MaxQueryLength = 100;
    public const int MinQueryLength = 2;
    public const int ResultsPerGroup = 5;

    public async Task<Result<SiteSearchResponse>> SearchAsync(string? languageCode, string? query, CancellationToken cancellationToken)
    {
        if (languageCode is null || !SupportedLanguages.All.Contains(languageCode))
        {
            return Error.Validation("Search.UnsupportedLanguage", "Unsupported language.");
        }

        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length > MaxQueryLength)
        {
            trimmed = trimmed[..MaxQueryLength];
        }

        var parsed = SearchTextNormalizer.ParseQuery(trimmed);
        if (parsed.Words.Length < MinQueryLength)
        {
            return new SiteSearchResponse(trimmed, languageCode, 0, []);
        }

        // Any excluded word rules a page out (each word joined with "or"; the words are letters and digits only).
        var excluded = parsed.ExcludedWords.Length == 0 ? null : string.Join(" or ", parsed.ExcludedWords.Split(' '));
        var hits = await engine.SearchAsync(
            new SiteSearchQuery(languageCode, parsed.Words, parsed.WebQuery, excluded, PrefixTerm(parsed.Words), ResultsPerGroup), cancellationToken).ConfigureAwait(false);
        var groups = hits
            .GroupBy(hit => hit.Group, StringComparer.Ordinal)
            .OrderBy(group => Order(group.Key))
            .Select(group => new SearchResultGroupResponse(
                group.Key,
                group.Max(hit => hit.GroupTotal),
                group.OrderByDescending(hit => hit.Score).Take(ResultsPerGroup)
                    .Select(hit => new SearchResultItemResponse(hit.Title, hit.Summary, hit.Path, hit.ContentType)).ToList()))
            .ToList();
        return new SiteSearchResponse(trimmed, languageCode, groups.Sum(group => group.Total), groups);
    }

    /// <summary>The last word, if it is letters and digits only (anything else is never turned into query syntax).</summary>
    public static string? PrefixTerm(string normalized)
    {
        var last = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return last is { Length: >= MinQueryLength } && last.All(char.IsLetterOrDigit) ? last : null;
    }

    private static int Order(string group)
    {
        var index = SearchGroups.Ordered.ToList().IndexOf(group);
        return index < 0 ? int.MaxValue : index;
    }
}
