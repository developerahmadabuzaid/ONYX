using Onyx.Application.Localization;
using Onyx.Contracts.Content;
using Onyx.Contracts.News;
using Onyx.Domain.Common;
using Onyx.Domain.News;

namespace Onyx.Application.News;

/// <summary>
/// The public News Hub (architecture.md §17): only <see cref="NewsItemStatus.Published"/> items are ever returned. Every item
/// carries its attribution (publisher, original link, the source's time when known, Onyx's publication time). Items are
/// <c>noindex, follow</c> unless an editor marked them indexable; only indexable items, and the hub and categories that have
/// published items, are listed in the sitemap.
/// </summary>
public sealed class PublicNewsService(INewsItemRepository items, INewsCategoryRepository categories)
{
    public const int PageSize = 20;
    public const int MaxLatest = 4;
    public const int MaxSitemapItems = 5_000;

    private static readonly Error UnsupportedLanguage = Error.Validation("News.UnsupportedLanguage", "Unsupported language.");

    public async Task<Result<PublicNewsListResponse>> GetHubAsync(string? languageCode, int page, CancellationToken cancellationToken)
    {
        if (!IsSupported(languageCode))
        {
            return UnsupportedLanguage;
        }

        var allCategories = await categories.ListAsync(cancellationToken).ConfigureAwait(false);
        return await ListAsync(languageCode!, null, allCategories, page, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A slug directly below the hub: a category page, a published item, or nothing.</summary>
    public async Task<Result<NewsResolveResponse>> ResolveAsync(string? languageCode, string? slug, int page, CancellationToken cancellationToken)
    {
        if (!IsSupported(languageCode))
        {
            return UnsupportedLanguage;
        }

        var language = languageCode!;
        var cleanSlug = Domain.Content.Slug.Normalize(Uri.UnescapeDataString(slug ?? string.Empty));
        if (cleanSlug.Length == 0)
        {
            return new NewsResolveResponse(null, null);
        }

        var allCategories = await categories.ListAsync(cancellationToken).ConfigureAwait(false);
        var category = allCategories.FirstOrDefault(candidate => candidate.Translation(language)?.Slug == cleanSlug);
        if (category is not null)
        {
            return new NewsResolveResponse(await ListAsync(language, category, allCategories, page, cancellationToken).ConfigureAwait(false), null);
        }

        var item = await items.GetPublishedBySlugAsync(language, cleanSlug, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return new NewsResolveResponse(null, null);
        }

        var itemCategory = allCategories.FirstOrDefault(candidate => candidate.Id == item.CategoryId);
        var translation = item.Translation(language)!;
        var alternates = item.Indexable
            ? item.Translations.ToDictionary(other => other.LanguageCode, other => ItemPath(other.LanguageCode, other.Slug), StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        return new NewsResolveResponse(null, new PublicNewsItemResponse(
            translation.Title, translation.Summary, ItemPath(language, translation.Slug), item.Publisher, item.OriginalUrl, item.SourcePublishedAtUtc,
            item.PublishedAtUtc!.Value, itemCategory is null ? null : ToCategory(itemCategory, language), item.Indexable, alternates));
    }

    /// <summary>The homepage's LatestNews section (architecture.md §7): at most <see cref="MaxLatest"/> published items.</summary>
    public async Task<IReadOnlyList<PublicNewsItemSummaryResponse>> GetLatestAsync(string? languageCode, int count, CancellationToken cancellationToken)
    {
        if (!IsSupported(languageCode))
        {
            return [];
        }

        var allCategories = await categories.ListAsync(cancellationToken).ConfigureAwait(false);
        var page = await items.ListPublishedAsync(languageCode!, null, 1, Math.Clamp(count, 1, MaxLatest), cancellationToken).ConfigureAwait(false);
        return page.Items.Select(item => ToSummary(item, languageCode!, allCategories)).ToList();
    }

    public async Task<IReadOnlyList<SitemapEntryResponse>> GetSitemapAsync(string? languageCode, CancellationToken cancellationToken)
    {
        if (!IsSupported(languageCode))
        {
            return [];
        }

        var language = languageCode!;
        var latestByCategory = await items.LatestPublishedByCategoryAsync(language, cancellationToken).ConfigureAwait(false);
        if (latestByCategory.Count == 0)
        {
            return [];
        }

        var entries = new List<SitemapEntryResponse>
        {
            new(HubPath(language), latestByCategory.Values.Max(), SupportedLanguages.All.ToDictionary(other => other, HubPath, StringComparer.Ordinal)),
        };

        foreach (var category in await categories.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            if (latestByCategory.TryGetValue(category.Id, out var latest) && category.Translation(language) is { } translation)
            {
                entries.Add(new SitemapEntryResponse(
                    CategoryPath(language, translation.Slug),
                    latest,
                    category.Translations.ToDictionary(other => other.LanguageCode, other => CategoryPath(other.LanguageCode, other.Slug), StringComparer.Ordinal)));
            }
        }

        foreach (var item in await items.ListPublishedIndexableAsync(MaxSitemapItems, cancellationToken).ConfigureAwait(false))
        {
            if (item.Translation(language) is { } translation)
            {
                entries.Add(new SitemapEntryResponse(
                    ItemPath(language, translation.Slug),
                    item.UpdatedAtUtc ?? item.PublishedAtUtc,
                    item.Translations.ToDictionary(other => other.LanguageCode, other => ItemPath(other.LanguageCode, other.Slug), StringComparer.Ordinal)));
            }
        }

        return entries;
    }

    public static string HubPath(string languageCode) => $"/{languageCode}/{NewsRouteSegments.Hub[languageCode]}";

    public static string ItemPath(string languageCode, string slug) => $"{HubPath(languageCode)}/{slug}";

    private static string CategoryPath(string languageCode, string slug) => $"{HubPath(languageCode)}/{slug}";

    private async Task<PublicNewsListResponse> ListAsync(
        string language, NewsCategory? category, IReadOnlyList<NewsCategory> allCategories, int page, CancellationToken cancellationToken)
    {
        var current = Math.Max(page, 1);
        var result = await items.ListPublishedAsync(language, category?.Id, current, PageSize, cancellationToken).ConfigureAwait(false);
        var alternates = category is null
            ? SupportedLanguages.All.ToDictionary(other => other, HubPath, StringComparer.Ordinal)
            : category.Translations.ToDictionary(other => other.LanguageCode, other => CategoryPath(other.LanguageCode, other.Slug), StringComparer.Ordinal);
        return new PublicNewsListResponse(
            language,
            category is null ? null : ToCategory(category, language),
            result.Items.Select(item => ToSummary(item, language, allCategories)).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            allCategories.Where(candidate => candidate.Translation(language) is not null).Select(candidate => ToCategory(candidate, language)).ToList(),
            alternates);
    }

    private static PublicNewsItemSummaryResponse ToSummary(NewsItem item, string language, IReadOnlyList<NewsCategory> allCategories)
    {
        var translation = item.Translation(language)!;
        var category = allCategories.FirstOrDefault(candidate => candidate.Id == item.CategoryId)?.Translation(language);
        return new PublicNewsItemSummaryResponse(
            translation.Title, translation.Summary, ItemPath(language, translation.Slug), item.Publisher, category?.Name, item.SourcePublishedAtUtc, item.PublishedAtUtc!.Value);
    }

    private static PublicNewsCategoryResponse ToCategory(NewsCategory category, string language)
    {
        var translation = category.Translation(language)!;
        return new PublicNewsCategoryResponse(category.Key, translation.Name, translation.Slug, CategoryPath(language, translation.Slug));
    }

    private static bool IsSupported(string? languageCode) => languageCode is not null && SupportedLanguages.All.Contains(languageCode);
}
