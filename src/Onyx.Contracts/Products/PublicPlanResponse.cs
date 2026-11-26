namespace Onyx.Contracts.Products;

/// <summary><c>Price</c> and <c>Currency</c> are null unless an administrator entered a real price and marked it public.</summary>
public sealed record PublicPlanResponse(Guid Id, string Code, string Name, string? Description, decimal? Price, string? Currency, string BillingPeriod);
