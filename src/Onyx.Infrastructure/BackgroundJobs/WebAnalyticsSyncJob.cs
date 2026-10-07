using Onyx.Application.Analytics;
using Onyx.Application.Jobs;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily GA4 sync (architecture.md §15). Skipped while not configured or disabled; a provider failure fails the run (JobRun
/// error and notification) after recording the safe failure code, and the provider connection raises IntegrationFailed once.
/// </summary>
public sealed class WebAnalyticsSyncJob(AnalyticsSyncService sync) : IScheduledJob
{
    public string Name => AnalyticsIntegrationService.WebAnalyticsJob;

    public string DefaultSchedule => "daily@05:00";

    public async Task<JobOutcome> ExecuteAsync(CancellationToken cancellationToken) =>
        AnalyticsJobOutcome.From(await sync.SyncWebAnalyticsAsync(cancellationToken).ConfigureAwait(false));
}
