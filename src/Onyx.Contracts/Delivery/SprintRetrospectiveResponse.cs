namespace Onyx.Contracts.Delivery;

/// <summary>The team's retrospective notes for a sprint (plain text); all empty until someone records them.</summary>
public sealed record SprintRetrospectiveResponse(
    string? WentWell,
    string? ToImprove,
    string? ActionItems,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedByName);
