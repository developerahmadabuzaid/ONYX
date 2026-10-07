using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.Leads;
using Onyx.API.IntegrationTests.Worker;
using Onyx.Application.Analytics;
using Onyx.Application.Jobs;
using Onyx.Application.Providers;
using Onyx.Contracts.Analytics;
using Onyx.Domain.Common;

namespace Onyx.API.IntegrationTests.Analytics;

/// <summary>
/// Analytics test plumbing: the real Worker sync jobs with the two provider boundaries replaced by controlled fakes (no live
/// Google call in any test), and helpers for reports. All provider values are obviously fictional.
/// </summary>
public abstract class AnalyticsApiTestBase(PostgresFixture fixture) : LeadsApiTestBase(fixture)
{
    protected FakeWebAnalytics Web { get; } = new();

    protected FakeSearch Search { get; } = new();

    protected DateOnly Today => DateOnly.FromDateTime(Time.GetUtcNow().UtcDateTime);

    /// <summary>Runs one sync job like the scheduler would; a provider failure surfaces as the job's exception.</summary>
    protected async Task<JobOutcome> SyncAsync(string jobName, IReadOnlyDictionary<string, string?>? settings = null)
    {
        await using var worker = WorkerTestHost.Create(Fixture.ApplicationDatabase!.ConnectionString, "worker-a", Time, services =>
        {
            services.RemoveAll<IWebAnalyticsProvider>();
            services.AddSingleton<IWebAnalyticsProvider>(Web);
            services.RemoveAll<ISearchPerformanceProvider>();
            services.AddSingleton<ISearchPerformanceProvider>(Search);
        }, settings);
        try
        {
            return await worker.InScopeAsync(services => services.GetServices<IScheduledJob>().Single(job => job.Name == jobName).ExecuteAsync(Token));
        }
        finally
        {
            // Events raised before a failure (e.g. IntegrationFailed) are delivered like the Worker would.
            var processor = worker.Get<Onyx.Infrastructure.BackgroundJobs.OutboxProcessor>();
            while (await processor.ProcessBatchAsync(Token) > 0)
            {
            }
        }
    }

    protected static async Task<AnalyticsReportResponse> ReportAsync(HttpClient client, string path) =>
        await ReadAsync<AnalyticsReportResponse>(await client.GetAsync($"/api/v1/admin/{path}", Token));

    protected static AnalyticsSectionResponse Section(AnalyticsReportResponse report, string key) => report.Sections.Single(section => section.Key == key);

    protected static AnalyticsDataStatusResponse Source(AnalyticsReportResponse report, string provider) => report.Sources.Single(source => source.ProviderKey == provider);

    /// <summary>Stands in for GA4 behind <see cref="IWebAnalyticsProvider"/>.</summary>
    protected sealed class FakeWebAnalytics : IWebAnalyticsProvider
    {
        public static readonly WebAnalyticsBatch Empty = new([], [], [], false);

        public bool Configured { get; set; } = true;

        public Func<AnalyticsFetchRequest, CancellationToken, Task<Result<WebAnalyticsBatch>>> Respond { get; set; } =
            (_, _) => Task.FromResult(Result.Success(Empty));

        public ConcurrentQueue<AnalyticsFetchRequest> Requests { get; } = new();

        public string ProviderKey => ProviderKeys.GoogleAnalytics;

        public bool IsConfigured => Configured;

        public Task<Result<WebAnalyticsBatch>> FetchAsync(AnalyticsFetchRequest request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            return Respond(request, cancellationToken);
        }

        public void Returns(WebAnalyticsBatch batch) => Respond = (_, _) => Task.FromResult(Result.Success(batch));
    }

    /// <summary>Stands in for Search Console behind <see cref="ISearchPerformanceProvider"/>.</summary>
    protected sealed class FakeSearch : ISearchPerformanceProvider
    {
        public static readonly SearchPerformanceBatch Empty = new([], [], [], [], false);

        public bool Configured { get; set; } = true;

        public Func<AnalyticsFetchRequest, CancellationToken, Task<Result<SearchPerformanceBatch>>> Respond { get; set; } =
            (_, _) => Task.FromResult(Result.Success(Empty));

        public ConcurrentQueue<AnalyticsFetchRequest> Requests { get; } = new();

        public string ProviderKey => ProviderKeys.SearchConsole;

        public bool IsConfigured => Configured;

        public Task<Result<SearchPerformanceBatch>> FetchAsync(AnalyticsFetchRequest request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            return Respond(request, cancellationToken);
        }

        public void Returns(SearchPerformanceBatch batch) => Respond = (_, _) => Task.FromResult(Result.Success(batch));
    }

    protected static Task<HttpResponseMessage> ChangeStageAsync(HttpClient admin, Guid leadId, string stage) =>
        admin.PutAsJsonAsync($"/api/v1/admin/leads/{leadId}/stage", new Onyx.Contracts.Leads.ChangeLeadStageRequest(stage, null), Token);
}
