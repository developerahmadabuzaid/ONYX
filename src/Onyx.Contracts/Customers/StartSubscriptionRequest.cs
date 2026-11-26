namespace Onyx.Contracts.Customers;

/// <summary><c>Intent</c> is trial or subscribe (<see cref="CustomerIntents"/>).</summary>
public sealed record StartSubscriptionRequest(string ProductSlug, string Language, string Intent, Guid? PlanId, string? Note);
