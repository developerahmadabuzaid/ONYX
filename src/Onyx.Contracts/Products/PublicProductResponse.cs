namespace Onyx.Contracts.Products;

public sealed record PublicProductResponse(Guid Id, string Name, string Slug, string Summary, string Description, int? TrialDays, string DemoKind, string? DemoUrl, IReadOnlyList<PublicPlanResponse> Plans, IReadOnlyDictionary<string, string> Slugs);
