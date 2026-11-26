using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Analytics;
using Onyx.Application.Analytics;
using Onyx.Application.Providers;
using Onyx.Domain.Analytics;
using Onyx.Domain.Common;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Notifications;
using Onyx.Domain.Providers;
using Onyx.Infrastructure.BackgroundJobs;

namespace Onyx.API.IntegrationTests.Analytics;

/// <summary>GA4 and Search Console sync jobs on PostgreSQL with fake providers: states, bounds, idempotency, mapping and failures.</summary>
public sealed class AnalyticsSyncTests(PostgresFixture fixture) : AnalyticsApiTestBase(fixture)
{
    private static readonly string Ga4Job = AnalyticsIntegrationService.WebAnalyticsJob;
    private static readonly string GscJob = AnalyticsIntegrationService.SearchPerformanceJob;

    [Fact]
    public async Task Without_configuration_both_syncs_are_skipped_and_reports_say_not_configured_without_zeroes()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        Web.Configured = false;
        Search.Configured = false;

        Assert.True((await SyncAsync(Ga4Job)).WasSkipped);
        Assert.True((await SyncAsync(GscJob)).WasSkipped);

        Assert.Empty(Web.Requests);
        Assert.Empty(Search.Requests);
        Assert.All(await QueryAsync(db => db.ProviderConnections), connection => Assert.Equal(ProviderConnectionStatus.NotConfigured, connection.Status));
        var overview = await ReportAsync(owner, "analytics/overview");
        Assert.Equal("NotConfigured", Source(overview, ProviderKeys.GoogleAnalytics).State);
        Assert.Empty(Section(overview, "webDaily").Rows);
        var search = await ReportAsync(owner, "search-performance/overview");
        Assert.Equal("NotConfigured", Source(search, ProviderKeys.SearchConsole).State);
        Assert.All(search.Sections, section => Assert.Empty(section.Rows));
    }

    [Fact]
    public async Task A_sync_stores_bounded_aggregates_maps_pages_per_language_and_is_idempotent()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await PublishAsync(owner, await CreatePageAsync(owner));
        var day = Today.AddDays(-1);
        Web.Returns(new WebAnalyticsBatch(
            [new(day, 10, 20, 12, 50), new(day.AddDays(-1), 5, 8, 4, 16)],
            [
                new(day, "/en/about-us?utm_source=sample", "google", "organic", "(not set)", "Saudi Arabia", "mobile", 6, 3, 10),
                new(day, "/en/about-us", "google", "organic", "(not set)", "Saudi Arabia", "mobile", 4, 2, 5),
                new(day, "/ar/%D8%B5%D9%81%D8%AD%D8%A9-%D8%AA%D8%B9%D8%B1%D9%8A%D9%81%D9%8A%D8%A9", "newsletter", "email", "spring-sample", "Jordan", "desktop", 7, 5, 9),
            ],
            [new(day, ConversionEvents.InquirySubmitted, 3), new(day, "page_view", 999)],
            false));

        var first = await SyncAsync(Ga4Job);
        var request = Assert.Single(Web.Requests);
        Assert.Equal(day, request.To);
        Assert.Equal(day.AddDays(1 - AnalyticsSettings.Default.InitialDays), request.From);
        Assert.Equal(ConversionEvents.All, request.ConversionEvents);
        Assert.False(first.WasSkipped);

        var rows = await QueryAsync(db => db.WebAnalyticsDailyMetrics.OrderBy(metric => metric.Kind).ThenBy(metric => metric.LandingPage));
        Assert.Equal(5, rows.Count);                                                                      // 2 totals, 2 acquisition (merged), 1 event
        var english = rows.Single(row => row.LandingPage == "/en/about-us");
        var arabic = rows.Single(row => row.LandingPage == "/ar/صفحة-تعريفية");
        Assert.Equal(10, english.Sessions);                                                               // query string dropped, rows merged
        Assert.Equal(("en", page.Id), (english.LanguageCode, english.ContentItemId!.Value));
        Assert.Equal(("ar", page.Id), (arabic.LanguageCode, arabic.ContentItemId!.Value));
        Assert.DoesNotContain(rows, row => row.EventName == "page_view");                                 // only Onyx's conversion events

        // Idempotent: the same days synced again replace, never duplicate.
        Time.Advance(TimeSpan.FromMinutes(1));
        await SyncAsync(Ga4Job);
        Assert.Equal(5, (await QueryAsync(db => db.WebAnalyticsDailyMetrics)).Count);

        var content = await ReportAsync(owner, "analytics/content");
        Assert.Equal("Ok", Source(content, ProviderKeys.GoogleAnalytics).State);
        Assert.Equal(["en", "ar"], Section(content, "ga4Content").Rows.Select(row => row.LanguageCode));
        Assert.Contains(Section(content, "ga4Content").Rows, row => row.Label == "Sample page" && row.Values[0] == 10);
        Assert.Contains(Section(content, "ga4Content").Rows, row => row.Label == "صفحة تجريبية" && row.Values[0] == 7);
        var overview = await ReportAsync(owner, "analytics/overview");
        var total = Section(overview, "webDaily").Rows.Single(row => row.Label == "total");
        Assert.Equal([null, 28, 16, 66], total.Values);                                                    // users never summed across days
    }

    [Fact]
    public async Task Search_rows_keep_languages_apart_map_to_content_and_feed_weekly_content_snapshots()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await PublishAsync(owner, await CreatePageAsync(owner));
        var day = Today.AddDays(-10);   // inside a complete Monday–Sunday week of the synced window
        Search.Returns(new SearchPerformanceBatch(
            [new(day, null, null, null, null, 9, 400, 0.0225, 8.5)],
            [
                new(day, "https://onyx.test/en/about-us", null, null, null, 6, 300, 0.02, 7.2),
                new(day, "https://onyx.test/ar/%D8%B5%D9%81%D8%AD%D8%A9-%D8%AA%D8%B9%D8%B1%D9%8A%D9%81%D9%8A%D8%A9", null, null, null, 3, 100, 0.03, 12.4),
            ],
            [new(day, null, "onyx sample query", null, null, 2, 150, 0.0133, 11)],
            [new(day, null, null, "sau", "MOBILE", 9, 400, 0.0225, 8.5)],
            false));

        await SyncAsync(GscJob);

        var request = Assert.Single(Search.Requests);
        Assert.Equal(Today.AddDays(-3), request.To);   // search data is final only after a few days
        var pages = await ReportAsync(owner, "search-performance/pages");
        var rows = Section(pages, "searchPages").Rows;
        Assert.Equal(2, rows.Count);
        var english = rows.Single(row => row.LanguageCode == "en");
        Assert.StartsWith("Sample page", english.Label, StringComparison.Ordinal);
        Assert.Equal([6, 300, 0.02, 7.2], english.Values);
        Assert.Equal([3, 100, 0.03, 12.4], rows.Single(row => row.LanguageCode == "ar").Values);

        var snapshots = await QueryAsync(db => db.ContentPerformanceSnapshots.Where(snapshot => snapshot.ContentItemId == page.Id && snapshot.Source == EvidenceSource.SearchConsole));
        Assert.Equal(["ar", "en"], snapshots.Select(snapshot => snapshot.LanguageCode).Order());
        Assert.Equal(6, snapshots.Single(snapshot => snapshot.LanguageCode == "en").Metrics["clicks"]);
        Assert.All(snapshots, snapshot => Assert.Equal(7, (snapshot.PeriodEndUtc - snapshot.PeriodStartUtc).TotalDays));

        // Re-running replaces the week's snapshots instead of adding more.
        await SyncAsync(GscJob);
        Assert.Equal(2, (await QueryAsync(db => db.ContentPerformanceSnapshots.Where(snapshot => snapshot.Source == EvidenceSource.SearchConsole))).Count);
    }

    [Fact]
    public async Task Rows_beyond_the_daily_cap_are_summed_into_other_and_the_run_is_marked_truncated()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var day = Today.AddDays(-1);
        Web.Returns(new WebAnalyticsBatch([], Enumerable.Range(1, 15)
            .Select(index => new WebAnalyticsAcquisitionRow(day, $"/en/page-{index}", "google", "organic", "(not set)", "Jordan", "desktop", 100 - index, 1, 1)).ToList(), [], false));

        await SyncAsync(Ga4Job, new Dictionary<string, string?> { ["Analytics:Sync:MaxRowsPerDay"] = "10" });

        var rows = await QueryAsync(db => db.WebAnalyticsDailyMetrics.Where(metric => metric.Kind == WebAnalyticsReportKind.Acquisition));
        Assert.Equal(10, rows.Count);
        var other = rows.Single(row => row.LandingPage == AnalyticsDimensions.Other);
        Assert.Equal(Enumerable.Range(10, 6).Sum(index => 100 - index), other.Sessions);
        Assert.True((await ReportAsync(owner, "analytics/landing-pages")).Sources.Single(source => source.ProviderKey == ProviderKeys.GoogleAnalytics).LastRunTruncated);
    }

    [Fact]
    public async Task Connected_without_rows_is_no_data_and_a_failure_keeps_the_last_good_data_visible()
    {
        Fixture.RequireDatabase();
        await SyncAsync(Ga4Job);
        var empty = (await InScopeAsync(services => services.GetRequiredService<AnalyticsReportService>().OverviewAsync(new AnalyticsReportQuery(), Token))).Value;
        Assert.Equal("NoData", Source(empty, ProviderKeys.GoogleAnalytics).State);
        Assert.Empty(Section(empty, "webDaily").Rows);
        Assert.Equal(ProviderConnectionStatus.Active, (await QueryAsync(db => db.ProviderConnections)).Single().Status);

        Web.Returns(new WebAnalyticsBatch([new(Today.AddDays(-1), 3, 4, 2, 9)], [], [], false));
        Time.Advance(TimeSpan.FromHours(1));
        await SyncAsync(Ga4Job);

        Web.Respond = (_, _) => Task.FromResult(Result.Failure<WebAnalyticsBatch>(Error.Unavailable(AnalyticsProviderErrors.Unauthorized, "secret-looking provider text")));
        Time.Advance(TimeSpan.FromDays(3));
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => SyncAsync(Ga4Job));
        Assert.Contains(AnalyticsProviderErrors.Unauthorized, failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-looking", failure.Message, StringComparison.Ordinal);

        var connection = (await QueryAsync(db => db.ProviderConnections)).Single();
        Assert.Equal(ProviderConnectionStatus.Error, connection.Status);
        Assert.Equal(AnalyticsProviderErrors.Unauthorized, connection.LastError);
        Assert.Single(await QueryAsync(db => db.AdminNotifications.Where(notification => notification.Type == NotificationType.IntegrationFailed)));

        var (_, owner) = await SignInSuperAdminAsync();
        var report = await ReportAsync(owner, $"analytics/overview?from={Today.AddDays(-10):yyyy-MM-dd}&to={Today:yyyy-MM-dd}");
        var status = Source(report, ProviderKeys.GoogleAnalytics);
        Assert.Equal("Failed", status.State);
        Assert.True(status.IsStale);
        Assert.Equal(AnalyticsProviderErrors.Unauthorized, status.LastFailureCode);
        Assert.NotEmpty(Section(report, "webDaily").Rows);   // the last good data is still shown, labelled as failed and stale
    }

    [Fact]
    public async Task A_provider_that_does_not_answer_in_time_fails_safely_and_a_disabled_provider_is_skipped()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        Search.Respond = async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Result.Success(FakeSearch.Empty);
        };

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => SyncAsync(GscJob, new Dictionary<string, string?> { ["Analytics:Sync:FetchTimeoutSeconds"] = "1" }));
        Assert.Contains(AnalyticsProviderErrors.Timeout, failure.Message, StringComparison.Ordinal);
        Assert.Equal(AnalyticsProviderErrors.Timeout, (await QueryAsync(db => db.AnalyticsSyncStates)).Single().LastFailureCode);

        Assert.Equal(System.Net.HttpStatusCode.OK, await StatusAsync(owner.PostAsync($"/api/v1/admin/provider-connections/{ProviderKeys.SearchConsole}/disable", null, Token)));
        Search.Returns(FakeSearch.Empty);
        Assert.True((await SyncAsync(GscJob)).WasSkipped);
        Assert.Equal("Disabled", Source(await ReportAsync(owner, "search-performance/overview"), ProviderKeys.SearchConsole).State);
    }
}
