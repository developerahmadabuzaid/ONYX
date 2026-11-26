namespace Onyx.Contracts.Delivery;

/// <summary>A link seen from one issue: Direction is "outward" (this issue is the source) or "inward".</summary>
public sealed record IssueLinkResponse(
    Guid Id,
    string Type,
    string Direction,
    Guid IssueId,
    string IssueKey,
    string IssueTitle,
    string StatusCategory);
