namespace Onyx.Contracts.Delivery;

/// <summary>New start and due dates for an issue (calendar and timeline scheduling). Null clears a date.</summary>
public sealed record RescheduleIssueRequest(DateOnly? StartDate, DateOnly? DueDate, uint Version);
