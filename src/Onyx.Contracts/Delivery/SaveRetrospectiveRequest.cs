namespace Onyx.Contracts.Delivery;

/// <summary>Saves a sprint's retrospective notes.</summary>
public sealed record SaveRetrospectiveRequest(string? WentWell, string? ToImprove, string? ActionItems, uint Version);
