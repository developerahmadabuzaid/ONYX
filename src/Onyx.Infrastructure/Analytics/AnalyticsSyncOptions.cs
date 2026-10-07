using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.Analytics;

/// <summary>Bound from <c>Analytics:Sync</c>. Technical bounds (see <see cref="Application.Analytics.AnalyticsSettings"/>); defaults pending owner confirmation.</summary>
public sealed class AnalyticsSyncOptions
{
    public const string SectionName = "Analytics:Sync";

    [Range(1, 480)]
    public int InitialDays { get; set; } = 28;

    [Range(1, 30)]
    public int RefreshDays { get; set; } = 3;

    [Range(1, 93)]
    public int MaxDaysPerRun { get; set; } = 31;

    [Range(100, 250_000)]
    public int MaxRowsPerReport { get; set; } = 25_000;

    [Range(10, 5_000)]
    public int MaxRowsPerDay { get; set; } = 500;

    [Range(1, 7)]
    public int WebAnalyticsDelayDays { get; set; } = 1;

    [Range(1, 7)]
    public int SearchDelayDays { get; set; } = 3;

    [Range(1, 900)]
    public int FetchTimeoutSeconds { get; set; } = 120;

    [Range(1, 720)]
    public int StaleAfterHours { get; set; } = 48;
}
