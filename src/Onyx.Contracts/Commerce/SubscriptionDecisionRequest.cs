namespace Onyx.Contracts.Commerce;

/// <summary><c>Decision</c>: activate, reject or cancel.</summary>
public sealed record SubscriptionDecisionRequest(string Decision, DateOnly? EndsOn, string? Note, uint Version);
