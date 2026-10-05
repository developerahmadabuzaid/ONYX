namespace Onyx.Contracts.Booking;

/// <summary>An iCalendar (RFC 5545) file.</summary>
public sealed record CalendarFileResponse(string FileName, string Content);
