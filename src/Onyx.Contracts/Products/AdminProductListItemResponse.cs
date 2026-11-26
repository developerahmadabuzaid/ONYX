namespace Onyx.Contracts.Products;

public sealed record AdminProductListItemResponse(Guid Id, string Key, string Name, string Status, bool IsVerified, int PlanCount, int SortOrder, DateTimeOffset? UpdatedAtUtc);
