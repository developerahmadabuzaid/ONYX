using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// Loop shared by the Worker's processors: keep going while there is work, sleep for the poll interval when idle.
/// An unexpected error is logged and the loop continues after the poll interval; it never crashes the host.
/// </summary>
public abstract partial class PollingBackgroundService(IOptions<WorkerOptions> options, ILogger logger) : BackgroundService
{
    protected abstract Task<int> RunOnceAsync(CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                processed = await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogLoopFailed(logger, exception, GetType().Name);
            }

            if (processed > 0)
            {
                continue;
            }

            try
            {
                await Task.Delay(options.Value.PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Service} iteration failed; retrying after the poll interval")]
    private static partial void LogLoopFailed(ILogger logger, Exception exception, string service);
}
