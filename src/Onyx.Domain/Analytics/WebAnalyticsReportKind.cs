namespace Onyx.Domain.Analytics;

/// <summary>Stored as text. Which provider report a web-analytics aggregate row belongs to (rows of different kinds never add up).</summary>
public enum WebAnalyticsReportKind
{
    /// <summary>One row per day: users, sessions, engaged sessions, views for the whole property.</summary>
    DailyTotal = 0,

    /// <summary>Sessions per day × landing page × source × medium × campaign × country × device (top rows per day, rest folded).</summary>
    Acquisition = 1,

    /// <summary>Counts per day of Onyx's own conversion events only.</summary>
    ConversionEvent = 2,
}
