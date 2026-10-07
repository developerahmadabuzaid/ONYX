namespace Onyx.Contracts.Analytics;

/// <summary>
/// Report period (inclusive days, UTC) and options. Defaults: the latest 28 days with data. <see cref="GroupBy"/> and
/// <see cref="Touch"/> apply to the first-party funnel (touch: <c>first</c> or <c>last</c>, default <c>last</c>).
/// </summary>
public sealed record AnalyticsReportQuery(DateOnly? From = null, DateOnly? To = null, string? GroupBy = null, string? Touch = null);
