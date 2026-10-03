using Onyx.Application.Jobs;

namespace Onyx.API.IntegrationTests.Worker;

public sealed class TestJob(TestJobProbe probe) : IScheduledJob
{
    public const string JobName = "test-job";

    public string Name => JobName;

    public string DefaultSchedule => "every:10m";

    public async Task<JobOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref probe.Executions);
        await Task.Delay(probe.Duration, cancellationToken);

        return probe.Mode switch
        {
            TestJobMode.Fail => throw new InvalidOperationException("Test job failure"),
            TestJobMode.Skip => JobOutcome.Skipped("Nothing to do"),
            _ => JobOutcome.Succeeded("Done"),
        };
    }
}
