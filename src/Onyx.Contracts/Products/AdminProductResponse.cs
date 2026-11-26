namespace Onyx.Contracts.Products;

public sealed record AdminProductResponse(Guid Id, string Key, string Status, int? TrialDays, string DemoKind, string? DemoUrl, int SortOrder, string? VerificationSource, DateTimeOffset? VerifiedAtUtc, string? VerifiedByName, DateTimeOffset? PublishedAtUtc, IReadOnlyList<ProductTranslationDto> Translations, IReadOnlyList<AdminPlanResponse> Plans, uint Version);
