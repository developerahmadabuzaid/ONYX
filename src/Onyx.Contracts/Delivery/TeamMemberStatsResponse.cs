namespace Onyx.Contracts.Delivery;

/// <summary>A person's open work by state, and what they completed in the selected period (issues assigned to them now).</summary>
public sealed record TeamMemberStatsResponse(
    Guid UserId,
    string DisplayName,
    int ToDo,
    int InProgress,
    int Overdue,
    int OpenPoints,
    int CompletedInRange);
