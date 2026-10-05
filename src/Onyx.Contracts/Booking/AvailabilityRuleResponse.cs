namespace Onyx.Contracts.Booking;

/// <summary>A weekly window; times are HH:mm in the business time zone.</summary>
public sealed record AvailabilityRuleResponse(Guid Id, string DayOfWeek, string StartTime, string EndTime);
