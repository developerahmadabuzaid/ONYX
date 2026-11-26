namespace Onyx.Application.Delivery;

/// <summary>An issue's position in a list (board column, sprint or backlog).</summary>
public sealed record IssueRankSlot(Guid IssueId, long Rank);
