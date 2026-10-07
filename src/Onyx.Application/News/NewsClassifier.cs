using Onyx.Application.Search;
using Onyx.Domain.News;

namespace Onyx.Application.News;

/// <summary>
/// Rule-based classification (architecture.md §17.3, no AI): a category wins with the most keyword matches in the entry's
/// title, feed categories and description, and a feed category named like an Onyx category counts extra. Without any match
/// the source's default category is used. Everything is compared after the shared search normalization, so Arabic spelling
/// variants and case do not matter.
/// </summary>
public static class NewsClassifier
{
    private const int NamedCategoryWeight = 3;

    public static Guid Classify(IReadOnlyList<NewsCategory> categories, Guid defaultCategoryId, FeedEntry entry)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(entry);
        var text = $" {SearchTextNormalizer.Normalize($"{entry.Title} {string.Join(' ', entry.Categories)} {entry.Excerpt}")} ";
        var feedCategories = entry.Categories.Select(SearchTextNormalizer.Normalize).Where(name => name.Length > 0).ToHashSet(StringComparer.Ordinal);

        Guid? best = null;
        var bestScore = 0;
        foreach (var category in categories.OrderBy(category => category.SortOrder))
        {
            var score = category.Keywords.Count(keyword => keyword.Length > 0 && text.Contains($" {keyword} ", StringComparison.Ordinal));
            if (category.Translations.Any(translation => feedCategories.Contains(SearchTextNormalizer.Normalize(translation.Name))))
            {
                score += NamedCategoryWeight;
            }

            if (score > bestScore)
            {
                best = category.Id;
                bestScore = score;
            }
        }

        return best ?? defaultCategoryId;
    }

    /// <summary>Normalized keywords as stored on a category (the same normalization as classification and search).</summary>
    public static IReadOnlyList<string> NormalizeKeywords(IEnumerable<string>? keywords) =>
        (keywords ?? []).Select(SearchTextNormalizer.Normalize).Where(keyword => keyword.Length > 0).Distinct(StringComparer.Ordinal).ToList();
}
