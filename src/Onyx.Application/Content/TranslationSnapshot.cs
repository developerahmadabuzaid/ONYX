using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public sealed record TranslationSnapshot(
    string LanguageCode,
    string Title,
    string Summary,
    string Slug,
    IReadOnlyList<ContentBlock> Body,
    IReadOnlyList<FaqEntry> Faqs,
    SeoProfile Seo);
