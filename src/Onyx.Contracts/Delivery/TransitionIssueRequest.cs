namespace Onyx.Contracts.Delivery;

/// <summary>Moves an issue to another status (validated against the workflow).</summary>
public sealed record TransitionIssueRequest(
    Guid StatusId,
    uint Version);
