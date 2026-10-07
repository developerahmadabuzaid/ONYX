using Onyx.Application.Search;
using Onyx.Domain.Common;
using Onyx.Domain.News;

namespace Onyx.Application.News;

/// <summary>
/// Onyx's own checks on a summary from the external service before it is stored (architecture.md §17.1: original, concise,
/// no verbatim reproduction). The service is trusted for nothing: languages, lengths, plain text and copying are verified here.
/// </summary>
public static class NewsSummaryChecks
{
    /// <summary>A run of this many consecutive words shared with the source text counts as copying.</summary>
    public const int CopiedRunWords = 8;

    public static Result Verify(NewsSummaryDraft draft, IReadOnlyCollection<string> languages, string originalTitle, string? sourceExcerpt)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(languages);
        var byLanguage = draft.Texts.GroupBy(text => text.LanguageCode, StringComparer.Ordinal).ToList();
        if (byLanguage.Any(group => group.Count() > 1) || !languages.All(language => byLanguage.Any(group => group.Key == language))
            || byLanguage.Any(group => !languages.Contains(group.Key)))
        {
            return Error.Validation("NewsSummary.Languages", "Exactly one headline and summary per site language is required.");
        }

        foreach (var text in draft.Texts)
        {
            var valid = NewsItem.Validate(text.Title, text.Summary);
            if (valid.IsFailure)
            {
                return valid;
            }

            if (ContainsLink(text.Title) || ContainsLink(text.Summary))
            {
                return Error.Validation("NewsSummary.Links", "Summaries must not contain links.");
            }

            if (CopiesSource(text.Summary, $"{originalTitle} {sourceExcerpt}") || CopiesSource(text.Title, sourceExcerpt ?? string.Empty))
            {
                return Error.Validation("NewsSummary.Verbatim", "The summary repeats the source text verbatim.");
            }
        }

        return Result.Success();
    }

    /// <summary>True when <paramref name="candidate"/> shares a run of <see cref="CopiedRunWords"/> consecutive words with the source.</summary>
    public static bool CopiesSource(string candidate, string source)
    {
        var words = SearchTextNormalizer.Normalize(candidate).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sourceText = $" {SearchTextNormalizer.Normalize(source)} ";
        if (words.Length < CopiedRunWords || sourceText.Length <= 2)
        {
            return false;
        }

        for (var start = 0; start + CopiedRunWords <= words.Length; start++)
        {
            if (sourceText.Contains($" {string.Join(' ', words, start, CopiedRunWords)} ", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsLink(string text) =>
        text.Contains("http://", StringComparison.OrdinalIgnoreCase) || text.Contains("https://", StringComparison.OrdinalIgnoreCase)
        || text.Contains("www.", StringComparison.OrdinalIgnoreCase);
}
