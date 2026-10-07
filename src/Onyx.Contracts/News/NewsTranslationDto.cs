namespace Onyx.Contracts.News;

public sealed record NewsTranslationDto(string LanguageCode, string Title, string Summary, string Slug);
