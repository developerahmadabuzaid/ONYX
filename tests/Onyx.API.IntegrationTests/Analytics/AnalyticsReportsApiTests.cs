using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.Worker;
using Onyx.Application.Analytics;
using Onyx.Application.Privacy;
using Onyx.Application.Providers;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Leads;
using Onyx.Contracts.Privacy;
using Onyx.Domain.Analytics;
using Onyx.Domain.Auditing;
using Onyx.Infrastructure.BackgroundJobs;

namespace Onyx.API.IntegrationTests.Analytics;

/// <summary>Admin analytics API: authorization, period bounds, the first-party funnel, aggregate-only comparisons, sync requests, retention.</summary>
public sealed class AnalyticsReportsApiTests(PostgresFixture fixture) : AnalyticsApiTestBase(fixture)
{
    private static readonly string[] AnalyticsReports = ["overview", "traffic", "landing-pages", "campaigns", "conversions", "content", "funnel"];
    private static readonly string[] SearchReports = ["overview", "queries", "pages", "signals"];

    [Fact]
    public async Task Reports_need_their_permission_and_are_never_cached()
    {
        Fixture.RequireDatabase();
        using var analyst = await SignInWithAsync("analyst", Permissions.Analytics.View);
        using var seo = await SignInWithAsync("seo", Permissions.SearchPerformance.View);
        using var editor = await SignInWithAsync("editor", Permissions.Content.View, Permissions.Leads.View);

        foreach (var report in AnalyticsReports)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync($"/api/v1/admin/analytics/{report}", Token)));
            Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.GetAsync($"/api/v1/admin/analytics/{report}", Token)));
            Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(seo.GetAsync($"/api/v1/admin/analytics/{report}", Token)));
            using var allowed = await analyst.GetAsync($"/api/v1/admin/analytics/{report}", Token);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            Assert.True(allowed.Headers.CacheControl?.NoStore);
        }

        foreach (var report in SearchReports)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync($"/api/v1/admin/search-performance/{report}", Token)));
            Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(analyst.GetAsync($"/api/v1/admin/search-performance/{report}", Token)));
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(seo.GetAsync($"/api/v1/admin/search-performance/{report}", Token)));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync("/api/v1/admin/analytics-integrations", Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(analyst.GetAsync("/api/v1/admin/analytics-integrations", Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(analyst.PostAsync($"/api/v1/admin/analytics-integrations/{ProviderKeys.GoogleAnalytics}/sync", null, Token)));
    }

    [Fact]
    public async Task Periods_are_bounded_and_groupings_validated()
    {
        Fixture.RequireDatabase();
        using var analyst = await SignInWithAsync("analyst", Permissions.Analytics.View);
        var today = Today;

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(analyst.GetAsync($"/api/v1/admin/analytics/overview?from={today:yyyy-MM-dd}&to={today.AddDays(-5):yyyy-MM-dd}", Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(analyst.GetAsync($"/api/v1/admin/analytics/overview?from={today.AddDays(-400):yyyy-MM-dd}&to={today:yyyy-MM-dd}", Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(analyst.GetAsync("/api/v1/admin/analytics/funnel?groupBy=email", Token)));
        var future = await ReportAsync(analyst, $"analytics/overview?from={today.AddDays(-3):yyyy-MM-dd}&to={today.AddDays(30):yyyy-MM-dd}");
        Assert.Equal(today, future.To);   // never reports days that have not happened
    }

    [Fact]
    public async Task The_funnel_counts_first_party_leads_by_attribution_and_current_stage()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await SubmitAsync(InquiryTypes.Contact, Form("first@example.com", attribution: Attribution("google", "cpc", "spring-sample", "/en/services")));
        await SubmitAsync(InquiryTypes.Contact, Form("second@example.com", attribution: Attribution("google", "cpc", "spring-sample", "/en/services")));
        await SubmitAsync(InquiryTypes.StartProject, Form("third@example.com", attribution: Attribution("newsletter", "email", "autumn-sample", "/en/")));
        await SubmitAsync(InquiryTypes.Contact, Form("fourth@example.com"));
        var leads = await QueryAsync(db => db.Leads.OrderBy(lead => lead.FirstInquiryAtUtc).Select(lead => lead.Id));
        await ChangeStageAsync(owner, leads[0], "Qualified");
        await ChangeStageAsync(owner, leads[2], "Won");

        var bySource = await ReportAsync(owner, "analytics/funnel?groupBy=source");
        var total = Section(bySource, "funnelTotal").Rows.Single();
        Assert.Equal([4, 2, 0, 1, 0, 1, 0], total.Values);   // leads, New, Contacted, Qualified, Proposal, Won, Lost
        var rows = Section(bySource, "funnelBy:source").Rows.ToDictionary(row => row.Label, row => row.Values);
        Assert.Equal([2, 1, 0, 1, 0, 0, 0], rows["google"]);
        Assert.Equal([1, 0, 0, 0, 0, 1, 0], rows["newsletter"]);
        Assert.Equal([1, 1, 0, 0, 0, 0, 0], rows["(not set)"]);
        Assert.Equal("Ok", Source(bySource, AnalyticsReportService.FirstParty).State);

        var byLanding = await ReportAsync(owner, "analytics/funnel?groupBy=landingpage");
        Assert.Equal(2, Section(byLanding, "funnelBy:landingpage").Rows.Single(row => row.Label == "/en/services").Values[0]);
    }

    [Fact]
    public async Task Ga4_and_first_party_data_are_only_compared_in_aggregate_by_a_shared_key()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var day = Today.AddDays(-1);
        Web.Returns(new WebAnalyticsBatch([new(day, 30, 40, 20, 90)],
            [
                new(day, "/en/services", "google", "cpc", "spring-sample", "Saudi Arabia", "desktop", 25, 12, 40),
                new(day, "/en/about-us", "google", "organic", "(not set)", "Jordan", "mobile", 150, 60, 200),
                new(day, "/en/", "partner", "referral", "ga4-only-sample", "Jordan", "mobile", 9, 4, 12),
            ],
            [new(day, ConversionEvents.InquirySubmitted, 1)], false));
        await SyncAsync(AnalyticsIntegrationService.WebAnalyticsJob);
        await SubmitAsync(InquiryTypes.Contact, Form("first@example.com", attribution: Attribution("google", "cpc", "spring-sample", "/en/services")));
        await SubmitAsync(InquiryTypes.Contact, Form("second@example.com", attribution: Attribution("newsletter", "email", "onyx-only-sample", "/en/")));

        var campaigns = await ReportAsync(owner, $"analytics/campaigns?from={day.AddDays(-7):yyyy-MM-dd}&to={Today:yyyy-MM-dd}");
        var comparison = Section(campaigns, "campaignComparison").Rows;
        Assert.Equal(["spring-sample"], comparison.Select(row => row.Label));   // only campaigns present in both datasets
        Assert.Equal([25, 1, 0], comparison.Single().Values);
        Assert.Contains("Analytics.Note.SeparateDatasets", campaigns.Notes);

        var landing = await ReportAsync(owner, $"analytics/landing-pages?from={day.AddDays(-7):yyyy-MM-dd}&to={Today:yyyy-MM-dd}");
        Assert.Equal(["/en/about-us"], Section(landing, "signalTrafficWithoutInquiries").Rows.Select(row => row.Label));

        var conversions = await ReportAsync(owner, $"analytics/conversions?from={day.AddDays(-7):yyyy-MM-dd}&to={Today:yyyy-MM-dd}");
        var submitted = Section(conversions, "conversionComparison").Rows.Single(row => row.Label == ConversionEvents.InquirySubmitted);
        Assert.Equal([1, 2], submitted.Values);   // GA4 (consenting visitors only) vs every Onyx inquiry
        Assert.Contains("Analytics.Note.ConsentLimited", conversions.Notes);
    }

    [Fact]
    public async Task Sync_requests_make_the_worker_job_due_and_are_audited()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsync($"/api/v1/admin/analytics-integrations/{ProviderKeys.GoogleAnalytics}/sync", null, Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(owner.PostAsync("/api/v1/admin/analytics-integrations/other.provider/sync", null, Token)));

        await using (var worker = WorkerTestHost.Create(Fixture.ApplicationDatabase!.ConnectionString, "worker-a", Time))
        {
            await worker.Get<JobScheduler>().SynchronizeDefinitionsAsync(Token);
        }

        Assert.Equal(HttpStatusCode.Accepted, await StatusAsync(owner.PostAsync($"/api/v1/admin/analytics-integrations/{ProviderKeys.GoogleAnalytics}/sync", null, Token)));
        var job = (await QueryAsync(db => db.JobDefinitions.Where(definition => definition.Name == AnalyticsIntegrationService.WebAnalyticsJob))).Single();
        Assert.Equal(Time.GetUtcNow(), job.NextRunAtUtc);
        Assert.Contains(AuditAction.AnalyticsSyncRequested, await QueryAsync(db => db.AuditLogs.Select(log => log.Action)));

        var integrations = await ReadAsync<List<Onyx.Contracts.Analytics.AnalyticsIntegrationResponse>>(await owner.GetAsync("/api/v1/admin/analytics-integrations", Token));
        Assert.Equal([ProviderKeys.GoogleAnalytics, ProviderKeys.SearchConsole], integrations.Select(integration => integration.ProviderKey));
        Assert.All(integrations, integration => Assert.Equal("NotConfigured", integration.ConnectionStatus));
    }

    [Fact]
    public async Task Analytics_aggregates_follow_an_owner_set_retention_policy_and_hold_no_personal_data()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var day = Today.AddDays(-1);
        Web.Returns(new WebAnalyticsBatch([new(day, 3, 4, 2, 9), new(day.AddDays(-20), 1, 1, 1, 1)], [], [], false));
        await SyncAsync(AnalyticsIntegrationService.WebAnalyticsJob);

        var policies = await ReadAsync<List<RetentionPolicyResponse>>(await owner.GetAsync("/api/v1/admin/retention-policies", Token));
        var analytics = policies.Single(policy => policy.Category == "AnalyticsAggregates");
        Assert.Null(analytics.PeriodDays);
        Assert.False(analytics.IsActive);
        Assert.Equal(["Delete"], analytics.AllowedActions);

        var preview = await ReadAsync<RetentionPreviewResponse>(await owner.GetAsync("/api/v1/admin/retention-policies/AnalyticsAggregates/preview?action=Delete&periodDays=10", Token));
        Assert.Equal(1, preview.EligibleCount);
        await ReadAsync<RetentionPolicyResponse>(await owner.PutAsJsonAsync("/api/v1/admin/retention-policies/AnalyticsAggregates",
            new SaveRetentionPolicyRequest("Delete", 10, true, null), Token));
        await InScopeAsync(services => services.GetRequiredService<IRetentionService>().EnforceAsync(Token));

        Assert.Equal([day], await QueryAsync(db => db.WebAnalyticsDailyMetrics.Select(metric => metric.Date)));
        Assert.Equal(Today.AddDays(-10), (await QueryAsync(db => db.AnalyticsSyncStates)).Single().DataFromDate);

        // The aggregate tables have no column for IP addresses, client or user ids, emails or names.
        await using var db = await DbAsync();
        var columns = await db.Database.SqlQuery<string>($"""
            SELECT column_name::text AS "Value" FROM information_schema.columns
            WHERE table_name IN ('web_analytics_daily_metrics', 'search_performance_daily_metrics', 'analytics_sync_states')
            """).ToListAsync(Token);
        Assert.NotEmpty(columns);
        Assert.DoesNotContain(columns, column => column.Contains("ip", StringComparison.Ordinal) || column.Contains("client", StringComparison.Ordinal)
            || column.Contains("email", StringComparison.Ordinal) || column.Contains("user_id", StringComparison.Ordinal) || column.Contains("name", StringComparison.Ordinal) && column != "event_name");
    }

    private AttributionDto Attribution(string source, string medium, string campaign, string landing) =>
        new(new TouchPointDto(landing, null, source, medium, campaign, null, null, Time.GetUtcNow()), new TouchPointDto(landing, null, source, medium, campaign, null, null, Time.GetUtcNow()),
            "/en/contact", "SA", "desktop", null, true);
}
