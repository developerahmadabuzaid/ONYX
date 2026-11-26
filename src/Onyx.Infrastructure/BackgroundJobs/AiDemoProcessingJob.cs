using Onyx.Application.Intelligence;
using Onyx.Application.Jobs;
using Onyx.Application.Products;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// Processes queued AI demo runs outside any web request. No LLM provider is connected yet, so <see cref="ITextGenerationService"/>
/// is usually absent: queued runs then fail visibly ("not connected") instead of waiting forever.
/// </summary>
public sealed class AiDemoProcessingJob(AiDemoService demos, IEnumerable<ITextGenerationService> generators) : IScheduledJob
{
    public const string JobName = "ai-demo-processing";

    public string Name => JobName;

    public string DefaultSchedule => "every:1m";

    public async Task<JobOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        var processed = await demos.ProcessQueuedAsync(generators.FirstOrDefault(), cancellationToken).ConfigureAwait(false);
        return processed == 0 ? JobOutcome.Skipped("No queued demo runs.") : JobOutcome.Succeeded($"{processed} demo run(s) processed.");
    }
}
