using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Onyx.Application.Jobs;
using Onyx.Application.Notifications;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Notifications;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs code-defined <see cref="IScheduledJob"/>s on their persisted schedules. Due jobs are claimed with
/// <c>FOR UPDATE SKIP LOCKED</c> and a lease, so a job runs on exactly one Worker instance at a time.
/// </summary>
public sealed partial class JobScheduler(
    IServiceScopeFactory scopeFactory,
    FailureNotifier failureNotifier,
    WorkerIdentity identity,
    IOptions<WorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<JobScheduler> logger)
{
    private const int MaxJobsPerPass = 10;

    /// <summary>Registers any job not yet in the database (multi-instance safe). Existing schedules are never overwritten.</summary>
    public async Task SynchronizeDefinitionsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = timeProvider.GetUtcNow();

        foreach (var job in scope.ServiceProvider.GetServices<IScheduledJob>())
        {
            var schedule = JobSchedule.Parse(job.DefaultSchedule);
            var id = Guid.CreateVersion7();
            var nextRun = schedule.GetNextOccurrence(now);
            var name = job.Name;
            var expression = job.DefaultSchedule;

            await dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO job_definitions (id, name, schedule, is_enabled, next_run_at_utc, created_at_utc)
                VALUES ({id}, {name}, {expression}, TRUE, {nextRun}, {now})
                ON CONFLICT (name) DO NOTHING
                """,
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<int> RunDueJobsAsync(CancellationToken cancellationToken)
    {
        var claimed = await ClaimDueAsync(cancellationToken).ConfigureAwait(false);
        foreach (var definition in claimed)
        {
            await RunAsync(definition, cancellationToken).ConfigureAwait(false);
        }

        return claimed.Count;
    }

    private async Task<List<JobDefinition>> ClaimDueAsync(CancellationToken cancellationToken)
    {
        var owner = identity.InstanceId;
        var now = timeProvider.GetUtcNow();
        var leaseUntil = now.Add(options.Value.Lease);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.JobDefinitions
            .FromSql($"""
                UPDATE job_definitions
                SET locked_by = {owner}, locked_until_utc = {leaseUntil}
                WHERE id IN (
                    SELECT id FROM job_definitions
                    WHERE is_enabled
                      AND next_run_at_utc <= {now}
                      AND (locked_until_utc IS NULL OR locked_until_utc < {now})
                    ORDER BY next_run_at_utc
                    LIMIT {MaxJobsPerPass}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RunAsync(JobDefinition definition, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var run = JobRun.Start(definition.Name, identity.InstanceId, startedAt);
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.JobRuns.Add(run);
            await dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }

        JobRunStatus status;
        string? message;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.Value.Lease);

            await using var scope = scopeFactory.CreateAsyncScope();
            var job = scope.ServiceProvider.GetServices<IScheduledJob>().FirstOrDefault(candidate => candidate.Name == definition.Name)
                ?? throw new InvalidOperationException($"No job named '{definition.Name}' is registered in this Worker.");

            var outcome = await job.ExecuteAsync(timeout.Token).ConfigureAwait(false);
            status = outcome.WasSkipped ? JobRunStatus.Skipped : JobRunStatus.Succeeded;
            message = outcome.Message;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Worker shutting down: record the interruption and release the lease without advancing the schedule, so the job re-runs soon.
            await FinishRunAsync(run.Id, JobRunStatus.Failed, "Interrupted by Worker shutdown.").ConfigureAwait(false);
            await ReleaseAsync(definition.Id, nextRunAtUtc: null, lastRunAtUtc: startedAt, JobRunStatus.Failed).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            LogJobFailed(logger, exception, definition.Name);
            status = JobRunStatus.Failed;
            message = exception is OperationCanceledException ? "Timed out." : $"{exception.GetType().Name}: {exception.Message}";
            await NotifyJobFailedAsync(definition.Name).ConfigureAwait(false);
        }

        var finishedAt = timeProvider.GetUtcNow();
        await FinishRunAsync(run.Id, status, message).ConfigureAwait(false);

        DateTimeOffset nextRun;
        if (JobSchedule.TryParse(definition.Schedule, out var schedule))
        {
            nextRun = schedule.GetNextOccurrence(finishedAt);
        }
        else
        {
            LogInvalidSchedule(logger, definition.Name, definition.Schedule);
            nextRun = finishedAt.AddHours(1);
            await NotifyJobFailedAsync(definition.Name).ConfigureAwait(false);
        }

        await ReleaseAsync(definition.Id, nextRun, startedAt, status).ConfigureAwait(false);
    }

    private async Task FinishRunAsync(Guid runId, JobRunStatus status, string? message)
    {
        var finishedAt = timeProvider.GetUtcNow();
        var truncated = message is { Length: > JobRun.MessageMaxLength } ? message[..JobRun.MessageMaxLength] : message;

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.JobRuns
            .Where(run => run.Id == runId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(run => run.Status, status)
                .SetProperty(run => run.Message, truncated)
                .SetProperty(run => run.FinishedAtUtc, finishedAt), CancellationToken.None)
            .ConfigureAwait(false);
    }

    /// <summary>Fenced on the lease owner. A null <paramref name="nextRunAtUtc"/> keeps the current schedule.</summary>
    private async Task ReleaseAsync(Guid definitionId, DateTimeOffset? nextRunAtUtc, DateTimeOffset lastRunAtUtc, JobRunStatus status)
    {
        var owner = identity.InstanceId;
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = dbContext.JobDefinitions.Where(job => job.Id == definitionId && job.LockedBy == owner);

        if (nextRunAtUtc is { } nextRun)
        {
            await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.NextRunAtUtc, nextRun)
                .SetProperty(job => job.LastRunAtUtc, lastRunAtUtc)
                .SetProperty(job => job.LastStatus, status)
                .SetProperty(job => job.LockedBy, (string?)null)
                .SetProperty(job => job.LockedUntilUtc, (DateTimeOffset?)null), CancellationToken.None).ConfigureAwait(false);
        }
        else
        {
            await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.LastRunAtUtc, lastRunAtUtc)
                .SetProperty(job => job.LastStatus, status)
                .SetProperty(job => job.LockedBy, (string?)null)
                .SetProperty(job => job.LockedUntilUtc, (DateTimeOffset?)null), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private Task NotifyJobFailedAsync(string jobName) =>
        failureNotifier.NotifyAsync(
            new NotificationRequest(
                NotificationType.JobFailed,
                NotificationSeverity.Warning,
                Permissions.SystemJobs.View,
                new Dictionary<string, string> { ["job"] = jobName },
                ResourceType: "Job",
                ResourceId: jobName),
            CancellationToken.None);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled job {JobName} failed")]
    private static partial void LogJobFailed(ILogger logger, Exception exception, string jobName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled job {JobName} has an invalid schedule '{Schedule}'")]
    private static partial void LogInvalidSchedule(ILogger logger, string jobName, string schedule);
}
