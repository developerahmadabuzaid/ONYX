namespace Onyx.Application.Analytics;

/// <summary>
/// Thresholds of the deterministic signals. They are heuristics that decide when a pattern is worth an editor's look, not
/// business facts or predictions; every signal shows its numbers and period so it can be judged on its own.
/// </summary>
public static class AnalyticsSignalRules
{
    /// <summary>A page or query needs at least this many impressions in the period before its CTR is judged.</summary>
    public const long MinImpressionsForCtr = 200;

    /// <summary>CTR below this (with enough impressions) is "low".</summary>
    public const double LowCtr = 0.01;

    /// <summary>Clicks in the previous period needed before a change is assessed.</summary>
    public const long MinClicksForTrend = 20;

    /// <summary>A change of at least this share (up or down) is reported.</summary>
    public const double TrendChange = 0.25;

    /// <summary>Average positions in this range with enough impressions are "near the first page".</summary>
    public const double OpportunityMinPosition = 8;

    public const double OpportunityMaxPosition = 20;

    public const long OpportunityMinImpressions = 100;

    /// <summary>Landing-page sessions (or search clicks) in the period before a page without any first-party inquiry is reported.</summary>
    public const long MinTrafficWithoutInquiries = 100;

    public const int MaxRowsPerSignal = 50;
}
