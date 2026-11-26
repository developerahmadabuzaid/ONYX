namespace Onyx.Contracts.Delivery;

/// <summary>Links this issue to another issue of the same project, identified by its key.</summary>
public sealed record CreateIssueLinkRequest(
    string? TargetKey,
    string? Type);
