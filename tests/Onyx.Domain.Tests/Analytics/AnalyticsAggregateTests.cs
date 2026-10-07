using Onyx.Domain.Analytics;
using Onyx.Domain.Privacy;

namespace Onyx.Domain.Tests.Analytics;

public sealed class AnalyticsAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 10, 5);

    [Fact]
    public void Dimensions_are_bounded_never_empty_and_keyed_by_a_stable_hash()
    {
        Assert.Equal(AnalyticsDimensions.NotSet, AnalyticsDimensions.Clean("  ", 10));
        Assert.Equal("a b", AnalyticsDimensions.Clean(" a \n b ", 10));
        Assert.Equal(10, AnalyticsDimensions.Clean(new string('x', 50), 10).Length);
        Assert.Equal(AnalyticsDimensions.Hash("Acquisition", "/en/", "google"), AnalyticsDimensions.Hash("Acquisition", "/en/", "google"));
        Assert.NotEqual(AnalyticsDimensions.Hash("Acquisition", "/en/", "google"), AnalyticsDimensions.Hash("Acquisition", "/ar/", "google"));
        Assert.NotEqual(AnalyticsDimensions.Hash("Acquisition", "a", "bc"), AnalyticsDimensions.Hash("Acquisition", "ab", "c"));
    }

    [Fact]
    public void Users_exist_only_on_daily_totals_and_metrics_are_never_negative()
    {
        var total = WebAnalyticsDailyMetric.DailyTotal("analytics.ga4", Day, 12, 20, 15, 40, Now);
        var acquisition = WebAnalyticsDailyMetric.Acquisition("analytics.ga4", Day, "/en/about-us", "en", "google", "organic", "(not set)", "Saudi Arabia", "mobile", null, -3, 2, 5, Now);
        var conversion = WebAnalyticsDailyMetric.ConversionEvent("analytics.ga4", Day, "inquiry_submitted", 4, Now);

        Assert.Equal(12, total.ActiveUsers);
        Assert.Null(acquisition.ActiveUsers);
        Assert.Equal(0, acquisition.Sessions);
        Assert.Equal("en", acquisition.LanguageCode);
        Assert.Equal(WebAnalyticsReportKind.ConversionEvent, conversion.Kind);
        Assert.Equal(4, conversion.EventCount);
        Assert.Equal(AnalyticsDimensions.HashLength, conversion.DimensionHash.Length);
    }

    [Fact]
    public void Search_rows_keep_only_their_own_dimensions_and_never_merge_languages()
    {
        var arabic = SearchPerformanceDailyMetric.Create("search.gsc", Day, SearchPerformanceReportKind.Page, "/ar/من-نحن", "ar", "ignored", "ignored", "ignored", null, 3, 100, 7.5, Now);
        var english = SearchPerformanceDailyMetric.Create("search.gsc", Day, SearchPerformanceReportKind.Page, "/en/about-us", "en", null, null, null, null, 5, 120, 6.1, Now);
        var query = SearchPerformanceDailyMetric.Create("search.gsc", Day, SearchPerformanceReportKind.Query, "/ignored", "en", "onyx", null, null, null, 1, 10, double.NaN, Now);

        Assert.Equal("ar", arabic.LanguageCode);
        Assert.Equal(string.Empty, arabic.Query);
        Assert.NotEqual(arabic.DimensionHash, english.DimensionHash);
        Assert.Equal(string.Empty, query.Page);
        Assert.Equal(string.Empty, query.LanguageCode);
        Assert.Equal(0, query.AveragePosition);
    }

    [Fact]
    public void Sync_state_tracks_coverage_failures_and_running_syncs()
    {
        var state = AnalyticsSyncState.For("search.gsc");
        state.Start(Now);
        Assert.True(state.IsSyncing);

        state.Succeed(Now.AddMinutes(1), Day.AddDays(-27), Day, 120, truncated: false);
        Assert.False(state.IsSyncing);
        state.Succeed(Now.AddDays(1), Day.AddDays(-2), Day.AddDays(1), 30, truncated: true);
        Assert.Equal(Day.AddDays(-27), state.DataFromDate);
        Assert.Equal(Day.AddDays(1), state.DataThroughDate);
        Assert.True(state.LastRunTruncated);

        state.Start(Now.AddDays(2));
        state.Fail(Now.AddDays(2).AddMinutes(1), "Analytics.Unauthorized");
        Assert.False(state.IsSyncing);
        Assert.Equal("Analytics.Unauthorized", state.LastFailureCode);
        Assert.Equal(Day.AddDays(1), state.DataThroughDate);
    }

    [Fact]
    public void Analytics_aggregates_have_a_delete_only_retention_category_without_a_preset_period()
    {
        Assert.Equal([RetentionAction.Delete], RetentionPolicy.AllowedActions(RetentionCategory.AnalyticsAggregates));
        Assert.Equal("Retention.PeriodRequired", RetentionPolicy.Create(RetentionCategory.AnalyticsAggregates, RetentionAction.Delete, null, isEnabled: true).Error!.Code);
        Assert.False(RetentionPolicy.Create(RetentionCategory.AnalyticsAggregates, RetentionAction.Delete, null, isEnabled: false).Value.IsActive);
        Assert.True(RetentionPolicy.Create(RetentionCategory.AnalyticsAggregates, RetentionAction.Anonymize, 30, isEnabled: false).IsFailure);
    }
}
