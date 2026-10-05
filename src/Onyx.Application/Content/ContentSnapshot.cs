using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>Everything a revision preserves: settings (including an insight's kind, origin and author) plus every language's text, body, FAQs and SEO profile.</summary>
public sealed record ContentSnapshot(
    ContentType Type,
    Guid? ParentId,
    string? Key,
    ContentPresentation Presentation,
    int SortOrder,
    Guid? PrimaryCtaId,
    Guid? FeaturedImageId,
    IReadOnlyList<TranslationSnapshot> Translations,
    InsightKind? InsightKind = null,
    InsightOrigin Origin = InsightOrigin.Human,
    Guid? AuthorId = null)
{
    public static ContentSnapshot From(ContentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ContentSnapshot(
            item.Type,
            item.ParentId,
            item.Key,
            item.Presentation,
            item.SortOrder,
            item.PrimaryCtaId,
            item.FeaturedImageId,
            item.Translations
                .OrderBy(translation => translation.LanguageCode, StringComparer.Ordinal)
                .Select(translation => new TranslationSnapshot(
                    translation.LanguageCode, translation.Title, translation.Summary, translation.Slug, translation.Body, translation.Faqs, translation.Seo))
                .ToList(),
            item.InsightKind,
            item.Origin,
            item.AuthorId);
    }

    public TranslationSnapshot? Translation(string languageCode) =>
        Translations.FirstOrDefault(translation => translation.LanguageCode == languageCode);
}
