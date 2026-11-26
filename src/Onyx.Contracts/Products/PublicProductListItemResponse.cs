namespace Onyx.Contracts.Products;

public sealed record PublicProductListItemResponse(Guid Id, string Name, string Slug, string Summary, int? TrialDays, string DemoKind, bool HasPublicPrice, IReadOnlyDictionary<string, string> Slugs);
