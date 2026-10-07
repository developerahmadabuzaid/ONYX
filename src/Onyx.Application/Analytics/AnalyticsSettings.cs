namespace Onyx.Application.Analytics;

/// <summary>
/// Technical bounds of the analytics sync (registered by the host from configuration). The backfill and refresh windows are
/// configurable technical defaults for the owner to confirm at activation; none is a business fact.
/// </summary>
/// <param name="InitialDays">Days loaded by the first successful sync.</param>
/// <param name="RefreshDays">Recent days re-loaded on every run (providers revise recent days).</param>
/// <param name="MaxDaysPerRun">Upper bound on days requested in one run.</param>
/// <param name="MaxRowsPerReport">Upper bound on rows read per provider report per run.</param>
/// <param name="MaxRowsPerDay">Dimension rows kept per day and report; smaller rows are summed into "(other)".</param>
/// <param name="WebAnalyticsDelayDays">Days before web-analytics data is synced (today is never final).</param>
/// <param name="SearchDelayDays">Days before search data is synced (search data arrives late).</param>
/// <param name="FetchTimeout">Time limit for one provider fetch.</param>
/// <param name="StaleAfter">A provider whose last success is older than this is shown as stale.</param>
public sealed record AnalyticsSettings(
    int InitialDays, int RefreshDays, int MaxDaysPerRun, int MaxRowsPerReport, int MaxRowsPerDay, int WebAnalyticsDelayDays, int SearchDelayDays,
    TimeSpan FetchTimeout, TimeSpan StaleAfter)
{
    public static AnalyticsSettings Default { get; } = new(28, 3, 31, 25_000, 500, 1, 3, TimeSpan.FromMinutes(2), TimeSpan.FromHours(48));
}
