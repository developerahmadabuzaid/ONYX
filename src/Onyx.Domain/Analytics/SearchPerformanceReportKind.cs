namespace Onyx.Domain.Analytics;

/// <summary>Stored as text. Which provider report a search-performance aggregate row belongs to (kinds never add up).</summary>
public enum SearchPerformanceReportKind
{
    /// <summary>One row per day for the whole property.</summary>
    DailyTotal = 0,

    /// <summary>Per day × page (top rows per day, rest folded).</summary>
    Page = 1,

    /// <summary>Per day × query (top rows per day, rest folded). The provider omits anonymized rare queries.</summary>
    Query = 2,

    /// <summary>Per day × country × device.</summary>
    CountryDevice = 3,
}
