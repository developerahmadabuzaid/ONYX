namespace Onyx.Contracts.Products;

public sealed record SaveProductRequest(int? TrialDays, string DemoKind, string? DemoUrl, int SortOrder, IReadOnlyList<ProductTranslationDto> Translations, uint Version);
