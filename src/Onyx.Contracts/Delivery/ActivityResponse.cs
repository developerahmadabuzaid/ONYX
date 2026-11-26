namespace Onyx.Contracts.Delivery;

/// <summary>A recent history entry across the projects the caller can see.</summary>
public sealed record ActivityResponse(
    Guid IssueId,
    string IssueKey,
    string IssueTitle,
    string Field,
    string? ActorName,
    DateTimeOffset AtUtc);
