namespace Onyx.Contracts.Products;

public sealed record SavePlanRequest(string? Code, decimal? Price, string? Currency, string BillingPeriod, bool IsPricePublic, bool IsActive, int SortOrder, IReadOnlyList<PlanTranslationDto> Translations, uint Version);
