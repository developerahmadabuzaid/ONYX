namespace Onyx.Contracts.Customers;

public sealed record CustomerSubscriptionResponse(Guid Id, Guid ProductId, string ProductName, string ProductSlug, string? PlanName, string Status, bool IsTrial, DateTimeOffset RequestedAtUtc, DateTimeOffset? TrialEndsAtUtc, DateTimeOffset? ActivatedAtUtc, DateOnly? EndsOn, uint Version);
