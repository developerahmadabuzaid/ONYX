using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Onyx.Infrastructure.BackgroundJobs;

public sealed class OutboxDispatcherService(OutboxProcessor processor, IOptions<WorkerOptions> options, ILogger<OutboxDispatcherService> logger)
    : PollingBackgroundService(options, logger)
{
    protected override Task<int> RunOnceAsync(CancellationToken cancellationToken) => processor.ProcessBatchAsync(cancellationToken);
}
