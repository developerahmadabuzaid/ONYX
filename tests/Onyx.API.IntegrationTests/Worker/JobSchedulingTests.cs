using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Jobs;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Notifications;
using Onyx.Infrastructure.BackgroundJobs;
using Onyx.Infrastructure.Persistence.Outbox;

namespace Onyx.API.IntegrationTests.Worker;

public sealed class JobSchedulingTests(PostgresFixture fixture) : WorkerDatabaseTestBase(fixture)
{
    private readonly TestJobProbe _probe = new();

    [Fact]
    public async Task Concurrent_synchronization_registers_each_job_once_with_its_default_schedule()
    {
        Fixture.RequireDatabase();
        await using var first = CreateHost("worker-a");
        await using var second = CreateHost("worker-b");

        await Task.WhenAll(
            first.Get<JobScheduler>().SynchronizeDefinitionsAsync(Token),
            second.Get<JobScheduler>().SynchronizeDefinitionsAsync(Token));

        var definitions = await ReadAsync(db => db.JobDefinitions);
        Assert.Equal(3, definitions.Count);
        var testJob = definitions.Single(job => job.Name == TestJob.JobName);
        Assert.Equal("every:10m", testJob.Schedule);
        Assert.True(testJob.IsEnabled);
        Assert.Equal(Start.AddMinutes(10), testJob.NextRunAtUtc);
        Assert.Contains(definitions, job => job.Name == OperationalCleanupJob.JobName && job.Schedule == "daily@03:00");
        Assert.Contains(definitions, job => job.Name == ContentSchedulingJob.JobName && job.Schedule == "every:1m");
    }

    [Fact]
    public async Task A_due_job_runs_on_exactly_one_worker_and_its_schedule_advances()
    {
        Fixture.RequireDatabase();
        _probe.Duration = TimeSpan.FromMilliseconds(300);
        await using var first = CreateHost("worker-a");
        await using var second = CreateHost("worker-b");
        await first.Get<JobScheduler>().SynchronizeDefinitionsAsync(Token);

        Time.Advance(TimeSpan.FromMinutes(10));
        await Task.WhenAll(first.Get<JobScheduler>().RunDueJobsAsync(Token), second.Get<JobScheduler>().RunDueJobsAsync(Token));

        Assert.Equal(1, _probe.Executions);
        var run = Assert.Single(await ReadAsync(db => db.JobRuns.Where(r => r.JobName == TestJob.JobName)));
        Assert.Equal(JobRunStatus.Succeeded, run.Status);
        Assert.Equal("Done", run.Message);
        Assert.NotNull(run.FinishedAtUtc);

        var definition = Assert.Single(await ReadAsync(db => db.JobDefinitions.Where(j => j.Name == TestJob.JobName)));
        Assert.Equal(JobRunStatus.Succeeded, definition.LastStatus);
        Assert.Equal(Start.AddMinutes(20), definition.NextRunAtUtc);
        Assert.Null(definition.LockedBy);

        // Not due again until the next occurrence.
        Assert.Equal(0, await first.Get<JobScheduler>().RunDueJobsAsync(Token));
    }

    [Fact]
    public async Task Failing_jobs_are_recorded_notified_and_still_rescheduled()
    {
        Fixture.RequireDatabase();
        _probe.Mode = TestJobMode.Fail;
        await using var host = CreateHost("worker-a");
        await host.Get<JobScheduler>().SynchronizeDefinitionsAsync(Token);

        Time.Advance(TimeSpan.FromMinutes(10));
        await host.Get<JobScheduler>().RunDueJobsAsync(Token);

        var run = Assert.Single(await ReadAsync(db => db.JobRuns.Where(r => r.JobName == TestJob.JobName)));
        Assert.Equal(JobRunStatus.Failed, run.Status);
        Assert.Contains("Test job failure", run.Message, StringComparison.Ordinal);

        var notification = Assert.Single(await ReadAsync(db => db.AdminNotifications));
        Assert.Equal(NotificationType.JobFailed, notification.Type);
        Assert.Equal(Permissions.SystemJobs.View, notification.RequiredPermission);
        Assert.Equal(TestJob.JobName, notification.Parameters["job"]);

        var definition = Assert.Single(await ReadAsync(db => db.JobDefinitions.Where(j => j.Name == TestJob.JobName)));
        Assert.Equal(JobRunStatus.Failed, definition.LastStatus);
        Assert.Equal(Start.AddMinutes(20), definition.NextRunAtUtc);
    }

    [Fact]
    public async Task Skipped_outcomes_are_recorded()
    {
        Fixture.RequireDatabase();
        _probe.Mode = TestJobMode.Skip;
        await using var host = CreateHost("worker-a");
        await host.Get<JobScheduler>().SynchronizeDefinitionsAsync(Token);

        Time.Advance(TimeSpan.FromMinutes(10));
        await host.Get<JobScheduler>().RunDueJobsAsync(Token);

        var run = Assert.Single(await ReadAsync(db => db.JobRuns.Where(r => r.JobName == TestJob.JobName)));
        Assert.Equal(JobRunStatus.Skipped, run.Status);
        Assert.Equal("Nothing to do", run.Message);
        Assert.Empty(await ReadAsync(db => db.AdminNotifications));
    }

    [Fact]
    public async Task Disabled_jobs_do_not_run()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost("worker-a");
        await host.Get<JobScheduler>().SynchronizeDefinitionsAsync(Token);
        await using (var context = CreateContext())
        {
            await context.JobDefinitions.Where(j => j.Name == TestJob.JobName)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.IsEnabled, false), Token);
        }

        Time.Advance(TimeSpan.FromHours(1));
        await host.Get<JobScheduler>().RunDueJobsAsync(Token);

        Assert.Equal(0, _probe.Executions);
    }

    [Fact]
    public async Task Operational_cleanup_removes_only_old_delivered_outbox_messages_and_old_job_runs()
    {
        Fixture.RequireDatabase();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await using (var context = CreateContext())
        {
            context.OutboxMessages.AddRange(
                OutboxMessage.FromDomainEvent(new TestOutboxEvent(Guid.NewGuid(), false), Start, options),
                OutboxMessage.FromDomainEvent(new TestOutboxEvent(Guid.NewGuid(), false), Start, options),
                OutboxMessage.FromDomainEvent(new TestOutboxEvent(Guid.NewGuid(), false), Start, options));
            await context.SaveChangesAsync(Token);

            var ids = await context.OutboxMessages.OrderBy(m => m.Id).Select(m => m.Id).ToListAsync(Token);
            var old = Start.AddDays(-30);
            var recent = Start.AddDays(-1);
            await context.OutboxMessages.Where(m => m.Id == ids[0]).ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessedAtUtc, old), Token);
            await context.OutboxMessages.Where(m => m.Id == ids[1]).ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessedAtUtc, recent), Token);
            await context.OutboxMessages.Where(m => m.Id == ids[2]).ExecuteUpdateAsync(s => s.SetProperty(m => m.DeadLetteredAtUtc, old), Token);

            var oldRun = JobRun.Start("old", "w", Start.AddDays(-60));
            oldRun.Finish(JobRunStatus.Succeeded, null, Start.AddDays(-60));
            var recentRun = JobRun.Start("recent", "w", Start.AddDays(-2));
            recentRun.Finish(JobRunStatus.Succeeded, null, Start.AddDays(-2));
            context.JobRuns.AddRange(oldRun, recentRun);
            await context.SaveChangesAsync(Token);
        }

        await using var host = CreateHost("worker-a");
        var outcome = await host.InScopeAsync(services =>
            services.GetServices<IScheduledJob>().Single(job => job.Name == OperationalCleanupJob.JobName).ExecuteAsync(Token));

        Assert.False(outcome.WasSkipped);
        var remaining = await ReadAsync(db => db.OutboxMessages);
        Assert.Equal(2, remaining.Count);
        Assert.Contains(remaining, m => m.DeadLetteredAtUtc != null);
        Assert.Equal(["recent"], (await ReadAsync(db => db.JobRuns)).Select(r => r.JobName).ToArray());
    }

    private WorkerTestHost CreateHost(string instanceId) =>
        WorkerTestHost.Create(ConnectionString, instanceId, Time, services =>
        {
            services.AddSingleton(_probe);
            services.AddScoped<IScheduledJob, TestJob>();
        });
}
