namespace Onyx.Contracts.Delivery;

/// <summary>Saves the current issue search under a name.</summary>
public sealed record SaveIssueFilterRequest(string? Name, string? Query);
