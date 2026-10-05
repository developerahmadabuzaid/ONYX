namespace Onyx.Contracts.Booking;

/// <summary><see cref="DayOfWeek"/> is an English day name (Sunday...Saturday); times are HH:mm.</summary>
public sealed record SaveAvailabilityRuleRequest(string DayOfWeek, string StartTime, string EndTime);
