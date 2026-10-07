namespace Onyx.Contracts.News;

/// <summary>Creates or updates a category; names are required in every site language. <see cref="Version"/> is required to update.</summary>
public sealed record SaveNewsCategoryRequest(
    string Key, int SortOrder, IReadOnlyList<string>? Keywords, IReadOnlyList<NewsCategoryTranslationDto> Translations, uint? Version);
