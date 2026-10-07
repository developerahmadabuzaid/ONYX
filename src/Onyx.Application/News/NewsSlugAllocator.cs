using System.Globalization;
using Onyx.Domain.Content;

namespace Onyx.Application.News;

/// <summary>
/// Item slugs: derived from Onyx's headline, unique per language among items, and never equal to a category slug (both live
/// one level below the hub). A numeric suffix resolves clashes.
/// </summary>
public sealed class NewsSlugAllocator(INewsItemRepository items, INewsCategoryRepository categories)
{
    private const int MaxAttempts = 50;

    /// <summary>Slugs already handed out in this unit of work (not yet saved).</summary>
    private readonly HashSet<(string Language, string Slug)> _reserved = [];

    public async Task<string?> AllocateAsync(string languageCode, string title, Guid itemId, CancellationToken cancellationToken)
    {
        var stem = Slug.Normalize(title);
        if (stem.Length == 0)
        {
            stem = "news";
        }

        if (stem.Length > Slug.RecommendedMaxLength)
        {
            stem = stem[..Slug.RecommendedMaxLength].TrimEnd('-');
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var candidate = attempt == 1 ? stem : $"{stem}-{attempt.ToString(CultureInfo.InvariantCulture)}";
            if (_reserved.Contains((languageCode, candidate))
                || await items.SlugTakenAsync(languageCode, candidate, itemId, cancellationToken).ConfigureAwait(false)
                || await categories.SlugTakenAsync(languageCode, candidate, null, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            _reserved.Add((languageCode, candidate));
            return candidate;
        }

        return null;
    }
}
