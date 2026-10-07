using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Analytics;

namespace Onyx.API.IntegrationTests.Analytics;

/// <summary>Deterministic search signals from real (fake-provider) aggregates; nothing invented, nothing changed automatically.</summary>
public sealed class SearchSignalsTests(PostgresFixture fixture) : AnalyticsApiTestBase(fixture)
{
    [Fact]
    public async Task Signals_follow_fixed_rules_on_the_period_against_the_previous_period()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await PublishAsync(owner, await CreatePageAsync(owner));
        var current = Today.AddDays(-4);
        var previous = current.AddDays(-7);
        Search.Returns(new SearchPerformanceBatch(
            [],
            [
                new(current, "https://onyx.test/en/low-ctr-sample", null, null, null, 2, 500, 0.004, 6),
                new(current, "https://onyx.test/en/declining-sample", null, null, null, 10, 300, 0.033, 4),
                new(previous, "https://onyx.test/en/declining-sample", null, null, null, 30, 300, 0.1, 4),
            ],
            [
                new(current, null, "near first page sample", null, null, 3, 150, 0.02, 12),
                new(current, null, "top result sample", null, null, 40, 150, 0.26, 1.2),
            ],
            [],
            false));
        await SyncAsync(AnalyticsIntegrationService.SearchPerformanceJob);

        var signals = await ReportAsync(owner, $"search-performance/signals?from={current.AddDays(-6):yyyy-MM-dd}&to={current:yyyy-MM-dd}");

        Assert.Contains(Section(signals, "signalHighImpressionsLowCtr").Rows, row => row.Label == "/en/low-ctr-sample");
        var declining = Assert.Single(Section(signals, "signalClicksDeclining").Rows);
        Assert.Equal(("/en/declining-sample", 10d, 30d), (declining.Label, declining.Values[0]!.Value, declining.Values[1]!.Value));
        Assert.Equal(["near first page sample"], Section(signals, "signalQueryOpportunities").Rows.Select(row => row.Label));
        Assert.Empty(Section(signals, "signalClicksImproving").Rows);
        Assert.Equal(["/ar/من-نحن", "/en/about-us"], Section(signals, "signalNoVisibility").Rows.Select(row => row.Label));
        Assert.Contains("Analytics.Note.Signals", signals.Notes);
    }
}
