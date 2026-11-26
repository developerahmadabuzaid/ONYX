namespace Onyx.Contracts.Delivery;

/// <summary>Sets or clears a status's work-in-progress limit.</summary>
public sealed record SetWipLimitRequest(
    int? WipLimit,
    uint Version);
