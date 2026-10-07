using Onyx.Application.Analytics;
using Onyx.Application.Jobs;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>Daily Search Console sync; same rules as <see cref="WebAnalyticsSyncJob"/>.</summary>
public sealed class SearchPerformanceSyncJob(AnalyticsSyncService sync) : IScheduledJob
{
    public string Name => AnalyticsIntegrationService.SearchPerformanceJob;

    public string DefaultSchedule => "daily@05:30";

    public async Task<JobOutcome> ExecuteAsync(CancellationToken cancellationToken) =>
        AnalyticsJobOutcome.From(await sync.SyncSearchPerformanceAsync(cancellationToken).ConfigureAwait(false));
}
