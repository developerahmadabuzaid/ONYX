namespace Onyx.Contracts.News;

/// <summary>A category name and slug in one language (an empty slug is derived from the name).</summary>
public sealed record NewsCategoryTranslationDto(string LanguageCode, string Name, string? Slug);
