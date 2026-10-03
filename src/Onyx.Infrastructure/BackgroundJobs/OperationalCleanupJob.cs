using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Onyx.Application.Jobs;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// Housekeeping for operational tables that hold no personal data: delivered outbox messages and old job-run history.
/// Dead-lettered messages are kept for investigation. Personal-data retention (emails, leads…) is handled by retention policies.
/// </summary>
public sealed class OperationalCleanupJob(ApplicationDbContext dbContext, IOptions<WorkerOptions> options, TimeProvider timeProvider) : IScheduledJob
{
    public const string JobName = "operational-cleanup";

    public string Name => JobName;

    public string DefaultSchedule => "daily@03:00";

    public async Task<JobOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var outboxCutoff = now.AddDays(-options.Value.ProcessedOutboxRetentionDays);
        var jobRunCutoff = now.AddDays(-options.Value.JobRunRetentionDays);

        var outboxRemoved = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc != null && message.ProcessedAtUtc < outboxCutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var runsRemoved = await dbContext.JobRuns
            .Where(run => run.FinishedAtUtc != null && run.FinishedAtUtc < jobRunCutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return JobOutcome.Succeeded($"Removed {outboxRemoved} delivered outbox messages and {runsRemoved} job runs.");
    }
}
