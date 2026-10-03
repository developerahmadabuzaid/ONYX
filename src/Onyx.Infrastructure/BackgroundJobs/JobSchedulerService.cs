using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Onyx.Infrastructure.BackgroundJobs;

public sealed class JobSchedulerService(JobScheduler scheduler, IOptions<WorkerOptions> options, ILogger<JobSchedulerService> logger)
    : PollingBackgroundService(options, logger)
{
    private bool _synchronized;

    protected override async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        // Retried on every pass until it succeeds (e.g. the database was briefly unavailable at startup).
        if (!_synchronized)
        {
            await scheduler.SynchronizeDefinitionsAsync(cancellationToken).ConfigureAwait(false);
            _synchronized = true;
        }

        return await scheduler.RunDueJobsAsync(cancellationToken).ConfigureAwait(false);
    }
}
