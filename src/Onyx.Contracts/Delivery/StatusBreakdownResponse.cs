namespace Onyx.Contracts.Delivery;

/// <summary>Issues by state, without overlap: overdue open issues are counted only as overdue.</summary>
public sealed record StatusBreakdownResponse(int Completed, int InProgress, int Pending, int Overdue);
