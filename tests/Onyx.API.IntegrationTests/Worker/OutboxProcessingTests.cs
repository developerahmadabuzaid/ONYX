using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Abstractions.Messaging;
using Onyx.Application.Providers;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Auditing;
using Onyx.Domain.Notifications;
using Onyx.Domain.Providers;
using Onyx.Infrastructure.BackgroundJobs;

namespace Onyx.API.IntegrationTests.Worker;

public sealed class OutboxProcessingTests(PostgresFixture fixture) : WorkerDatabaseTestBase(fixture)
{
    private readonly TestEventRecorder _recorder = new();

    [Fact]
    public async Task Two_workers_deliver_every_message_exactly_once()
    {
        Fixture.RequireDatabase();
        var events = Enumerable.Range(0, 40).Select(_ => new TestOutboxEvent(Guid.NewGuid(), ShouldFail: false)).ToArray();
        await SeedOutboxAsync(events);

        await using var first = CreateHost("worker-a", batchSize: 7);
        await using var second = CreateHost("worker-b", batchSize: 7);

        await Task.WhenAll(DrainAsync(first.Get<OutboxProcessor>()), DrainAsync(second.Get<OutboxProcessor>()));

        Assert.Equal(40, _recorder.Handled.Count);
        Assert.Equal(events.Select(e => e.EventId).Order(), _recorder.Handled.Order());
        var messages = await ReadAsync(db => db.OutboxMessages);
        Assert.All(messages, message =>
        {
            Assert.NotNull(message.ProcessedAtUtc);
            Assert.Null(message.LockedBy);
        });
    }

    [Fact]
    public async Task Failing_handlers_are_retried_with_backoff_then_dead_lettered_with_a_notification()
    {
        Fixture.RequireDatabase();
        await SeedOutboxAsync(new TestOutboxEvent(Guid.NewGuid(), ShouldFail: true));
        await using var host = CreateHost("worker-a");
        var processor = host.Get<OutboxProcessor>();

        await processor.ProcessBatchAsync(Token);

        var afterFirst = Assert.Single(await ReadAsync(db => db.OutboxMessages));
        Assert.Equal(1, afterFirst.AttemptCount);
        Assert.Null(afterFirst.DeadLetteredAtUtc);
        Assert.Equal(Start.AddSeconds(30), afterFirst.NextAttemptAtUtc);
        Assert.Contains(TestOutboxEventHandler.FailureMessage, afterFirst.LastError, StringComparison.Ordinal);

        // Not due yet: nothing is claimed.
        Assert.Equal(0, await processor.ProcessBatchAsync(Token));

        Time.Advance(TimeSpan.FromSeconds(31));
        await processor.ProcessBatchAsync(Token);
        Time.Advance(TimeSpan.FromSeconds(61));
        await processor.ProcessBatchAsync(Token);

        var deadLettered = Assert.Single(await ReadAsync(db => db.OutboxMessages));
        Assert.Equal(3, deadLettered.AttemptCount);
        Assert.NotNull(deadLettered.DeadLetteredAtUtc);
        Assert.Null(deadLettered.ProcessedAtUtc);

        var notification = Assert.Single(await ReadAsync(db => db.AdminNotifications));
        Assert.Equal(NotificationType.OutboxMessageFailed, notification.Type);
        Assert.Equal(Permissions.SystemJobs.View, notification.RequiredPermission);
        Assert.Equal(nameof(TestOutboxEvent), notification.Parameters["eventType"]);

        // Dead letters are never retried automatically.
        Time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(0, await processor.ProcessBatchAsync(Token));
    }

    [Fact]
    public async Task Unknown_event_types_are_dead_lettered_without_being_loaded()
    {
        Fixture.RequireDatabase();
        await using (var context = CreateContext())
        {
            var id = Guid.CreateVersion7();
            var occurred = Start;
            await context.Database.ExecuteSqlAsync(
                $"INSERT INTO outbox_messages (id, occurred_at_utc, type, payload, attempt_count) VALUES ({id}, {occurred}, 'System.Diagnostics.Process', '{{}}'::jsonb, 0)",
                Token);
        }

        await using var host = CreateHost("worker-a");
        await host.Get<OutboxProcessor>().ProcessBatchAsync(Token);

        var message = Assert.Single(await ReadAsync(db => db.OutboxMessages));
        Assert.NotNull(message.DeadLetteredAtUtc);
        Assert.Equal("Unknown event type.", message.LastError);
        Assert.Empty(_recorder.Handled);
    }

    [Fact]
    public async Task Expired_leases_are_reclaimed_but_live_leases_are_respected()
    {
        Fixture.RequireDatabase();
        var expired = new TestOutboxEvent(Guid.NewGuid(), false);
        var live = new TestOutboxEvent(Guid.NewGuid(), false);
        await SeedOutboxAsync(expired, live);

        await using (var context = CreateContext())
        {
            var messages = await context.OutboxMessages.ToListAsync(Token);
            var expiredId = messages.Single(m => m.Payload.Contains(expired.EventId.ToString(), StringComparison.Ordinal)).Id;
            var liveId = messages.Single(m => m.Payload.Contains(live.EventId.ToString(), StringComparison.Ordinal)).Id;
            var past = Start.AddMinutes(-1);
            var future = Start.AddMinutes(5);
            await context.OutboxMessages.Where(m => m.Id == expiredId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedBy, "crashed-worker").SetProperty(m => m.LockedUntilUtc, past), Token);
            await context.OutboxMessages.Where(m => m.Id == liveId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedBy, "busy-worker").SetProperty(m => m.LockedUntilUtc, future), Token);
        }

        await using var host = CreateHost("worker-a");
        await host.Get<OutboxProcessor>().ProcessBatchAsync(Token);

        Assert.Equal([expired.EventId], _recorder.Handled.ToArray());
    }

    [Fact]
    public async Task Provider_failure_notifies_once_through_the_outbox_and_is_audited()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost("worker-a");

        await host.InScopeAsync(async services =>
        {
            var providers = services.GetRequiredService<IProviderConnectionService>();
            await providers.ReportFailureAsync("analytics.ga4", ProviderCategory.Analytics, "Credentials rejected.", Token);
            await providers.ReportFailureAsync("analytics.ga4", ProviderCategory.Analytics, "Credentials rejected again.", Token);
            return 0;
        });

        var outbox = Assert.Single(await ReadAsync(db => db.OutboxMessages));
        Assert.Equal(typeof(ProviderConnectionFailed).FullName, outbox.Type);

        await host.Get<OutboxProcessor>().ProcessBatchAsync(Token);

        var notification = Assert.Single(await ReadAsync(db => db.AdminNotifications));
        Assert.Equal(NotificationType.IntegrationFailed, notification.Type);
        Assert.Equal(Permissions.ProviderConnections.Manage, notification.RequiredPermission);
        Assert.Equal("analytics.ga4", notification.Parameters["provider"]);

        var connection = Assert.Single(await ReadAsync(db => db.ProviderConnections));
        Assert.Equal(ProviderConnectionStatus.Error, connection.Status);
        Assert.Equal("Credentials rejected again.", connection.LastError);

        var audits = await ReadAsync(db => db.AuditLogs.Where(log => log.EntityType == nameof(ProviderConnection)));
        Assert.Contains(audits, log => log.Action == AuditAction.Created && log.Category == AuditCategory.Integrations);
    }

    private WorkerTestHost CreateHost(string instanceId, int batchSize = 50) =>
        WorkerTestHost.Create(
            ConnectionString,
            instanceId,
            Time,
            services =>
            {
                services.AddSingleton(_recorder);
                services.AddScoped<IDomainEventHandler<TestOutboxEvent>, TestOutboxEventHandler>();
            },
            new Dictionary<string, string?> { ["Worker:OutboxBatchSize"] = batchSize.ToString(System.Globalization.CultureInfo.InvariantCulture) });

    private static async Task DrainAsync(OutboxProcessor processor)
    {
        while (await processor.ProcessBatchAsync(Token) > 0)
        {
        }
    }
}
