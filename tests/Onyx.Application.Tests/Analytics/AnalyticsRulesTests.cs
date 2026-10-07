using Onyx.Application.Analytics;

namespace Onyx.Application.Tests.Analytics;

public sealed class AnalyticsRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 7);

    [Fact]
    public void The_first_sync_loads_the_initial_window_up_to_the_last_final_day()
    {
        var (from, through) = AnalyticsSyncService.Window(Today, delayDays: 3, lastSynced: null, AnalyticsSettings.Default);

        Assert.Equal(new DateOnly(2026, 10, 4), through);
        Assert.Equal(through.AddDays(1 - AnalyticsSettings.Default.InitialDays), from);
    }

    [Fact]
    public void Later_syncs_refresh_recent_days_and_long_gaps_catch_up_in_bounded_steps()
    {
        var refresh = AnalyticsSyncService.Window(Today, 1, lastSynced: new DateOnly(2026, 10, 5), AnalyticsSettings.Default);
        Assert.Equal((new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 6)), refresh);

        var gap = AnalyticsSyncService.Window(Today, 1, lastSynced: new DateOnly(2026, 5, 1), AnalyticsSettings.Default);
        Assert.Equal(new DateOnly(2026, 4, 29), gap.From);
        Assert.Equal(AnalyticsSettings.Default.MaxDaysPerRun, gap.Through.DayNumber - gap.From.DayNumber + 1);

        var upToDate = AnalyticsSyncService.Window(Today, 1, lastSynced: new DateOnly(2026, 10, 6), AnalyticsSettings.Default);
        Assert.True(upToDate.Through <= new DateOnly(2026, 10, 6));
    }

    [Theory]
    [InlineData("https://www.onyx.test/en/about-us/?utm_source=x#top", "/en/about-us", "en")]
    [InlineData("/ar/%D9%85%D9%86-%D9%86%D8%AD%D9%86", "/ar/من-نحن", "ar")]
    [InlineData("/ar", "/ar/", "ar")]
    [InlineData("/en/", "/en/", "en")]
    [InlineData("/", "/", "")]
    [InlineData("/fr/page", "/fr/page", "")]
    [InlineData("(not set)", "", "")]
    [InlineData(null, "", "")]
    public void Provider_pages_become_site_paths_with_their_own_language(string? value, string path, string language) =>
        Assert.Equal((path, language), AnalyticsPaths.Normalize(value));

    [Fact]
    public void Only_onyx_workflows_have_conversion_events()
    {
        Assert.Equal(["inquiry_submitted", "consultation_booking_confirmed"], ConversionEvents.All);
    }
}
