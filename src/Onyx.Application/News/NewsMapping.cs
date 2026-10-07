using Onyx.Contracts.News;
using Onyx.Domain.News;

namespace Onyx.Application.News;

internal static class NewsMapping
{
    public static NewsSourceResponse ToResponse(NewsSource source) =>
        new(
            source.Id, source.Name, source.HomepageUrl, source.FeedUrl, source.FeedFormat.ToString(), source.LanguageCode, source.DefaultCategoryId,
            source.Status.ToString(), source.LicensingNote, source.AutoPublish, source.FetchIntervalMinutes, source.NextFetchAtUtc, source.LastFetchedAtUtc,
            source.LastSucceededAtUtc, source.LastError, source.ConsecutiveFailures, source.Version);

    public static NewsCategoryResponse ToResponse(NewsCategory category) =>
        new(
            category.Id, category.Key, category.SortOrder, category.Keywords.ToList(),
            category.Translations.OrderBy(translation => translation.LanguageCode, StringComparer.Ordinal)
                .Select(translation => new NewsCategoryTranslationDto(translation.LanguageCode, translation.Name, translation.Slug)).ToList(),
            category.Version);

    public static NewsItemResponse ToResponse(NewsItem item) =>
        new(
            item.Id, item.SourceId, item.Publisher, item.OriginalUrl, item.OriginalTitle, item.SourceLanguageCode, item.SourcePublishedAtUtc, item.IngestedAtUtc,
            item.CategoryId, item.SimilarToItemId, item.Status.ToString(), item.HoldReason?.ToString(), item.SummaryOrigin?.ToString(), item.SummaryProvider,
            item.AutoPublished, item.Indexable, item.PublishedAtUtc, item.FirstPublishedAtUtc,
            item.Translations.OrderBy(translation => translation.LanguageCode, StringComparer.Ordinal)
                .Select(translation => new NewsTranslationDto(translation.LanguageCode, translation.Title, translation.Summary, translation.Slug)).ToList(),
            item.Version);

    public static NewsItemListItemResponse ToListItem(NewsItem item) =>
        new(
            item.Id, item.OriginalTitle, item.Publisher, item.Status.ToString(), item.HoldReason?.ToString(), item.CategoryId, item.SourcePublishedAtUtc,
            item.IngestedAtUtc, item.PublishedAtUtc, item.AutoPublished, item.Indexable,
            item.Translations.Select(translation => translation.LanguageCode).Order(StringComparer.Ordinal).ToList());
}
