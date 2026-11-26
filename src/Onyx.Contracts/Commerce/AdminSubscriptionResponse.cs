namespace Onyx.Contracts.Commerce;

public sealed record AdminSubscriptionResponse(Guid Id, Guid CustomerId, string CustomerName, string CustomerEmail, Guid ProductId, string ProductName, string? PlanName, string Status, bool IsTrial, DateTimeOffset RequestedAtUtc, DateTimeOffset? TrialEndsAtUtc, DateTimeOffset? ActivatedAtUtc, DateOnly? EndsOn, string? CustomerNote, string? StaffNote, uint Version);
