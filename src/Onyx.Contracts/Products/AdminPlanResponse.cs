namespace Onyx.Contracts.Products;

public sealed record AdminPlanResponse(Guid Id, string Code, decimal? Price, string? Currency, string BillingPeriod, bool IsPricePublic, bool IsActive, int SortOrder, IReadOnlyList<PlanTranslationDto> Translations);
