using Onyx.Application.Jobs;
using Onyx.Application.Privacy;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily retention enforcement (architecture.md §21). The scheduler's lease keeps runs from overlapping; each batch commits on
/// its own, so a run that stops part-way simply continues next time. Skipped while no policy is enabled with a period.
/// </summary>
public sealed class RetentionEnforcementJob(IRetentionService retention) : IScheduledJob
{
    public const string JobName = "retention-enforcement";

    public string Name => JobName;

    public string DefaultSchedule => "daily@02:30";

    public async Task<JobOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        var summaries = await retention.EnforceAsync(cancellationToken).ConfigureAwait(false);
        if (summaries.Count == 0)
        {
            return JobOutcome.Skipped("No retention policy is enabled with a period.");
        }

        return JobOutcome.Succeeded(string.Join("; ", summaries.Select(summary =>
            $"{summary.Category} {summary.Action}: {summary.Processed}{(summary.MoreRemaining ? " (more next run)" : string.Empty)}")));
    }
}
