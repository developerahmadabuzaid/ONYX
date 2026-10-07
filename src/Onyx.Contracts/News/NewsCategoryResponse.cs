namespace Onyx.Contracts.News;

public sealed record NewsCategoryResponse(Guid Id, string Key, int SortOrder, IReadOnlyList<string> Keywords, IReadOnlyList<NewsCategoryTranslationDto> Translations, uint Version);
