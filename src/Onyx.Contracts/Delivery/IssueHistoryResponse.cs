namespace Onyx.Contracts.Delivery;

/// <summary>One recorded change, values already resolved for display.</summary>
public sealed record IssueHistoryResponse(
    string Field,
    string? OldValue,
    string? NewValue,
    string? ActorName,
    DateTimeOffset AtUtc);
