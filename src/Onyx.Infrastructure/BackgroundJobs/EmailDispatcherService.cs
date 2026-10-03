using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Onyx.Infrastructure.BackgroundJobs;

public sealed class EmailDispatcherService(EmailProcessor processor, IOptions<WorkerOptions> options, ILogger<EmailDispatcherService> logger)
    : PollingBackgroundService(options, logger)
{
    protected override Task<int> RunOnceAsync(CancellationToken cancellationToken) => processor.ProcessBatchAsync(cancellationToken);
}
