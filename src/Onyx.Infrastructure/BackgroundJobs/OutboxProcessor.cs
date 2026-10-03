using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Onyx.Application.Jobs;
using Onyx.Application.Notifications;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Common;
using Onyx.Domain.Notifications;
using Onyx.Infrastructure.Persistence;
using Onyx.Infrastructure.Persistence.Outbox;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// Delivers outbox messages to domain event handlers: claim a batch with <c>FOR UPDATE SKIP LOCKED</c> and a lease,
/// dispatch each message in its own scope, then complete, reschedule with backoff, or dead-letter. At-least-once delivery.
/// </summary>
public sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    DomainEventTypeRegistry registry,
    FailureNotifier failureNotifier,
    WorkerIdentity identity,
    IOptions<WorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var owner = identity.InstanceId;
        var now = timeProvider.GetUtcNow();
        var leaseUntil = now.Add(settings.Lease);
        var batchSize = settings.OutboxBatchSize;

        List<OutboxMessage> claimed;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            claimed = await dbContext.OutboxMessages
                .FromSql($"""
                    UPDATE outbox_messages
                    SET locked_by = {owner}, locked_until_utc = {leaseUntil}
                    WHERE id IN (
                        SELECT id FROM outbox_messages
                        WHERE processed_at_utc IS NULL
                          AND dead_lettered_at_utc IS NULL
                          AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= {now})
                          AND (locked_until_utc IS NULL OR locked_until_utc < {now})
                        ORDER BY occurred_at_utc
                        LIMIT {batchSize}
                        FOR UPDATE SKIP LOCKED)
                    RETURNING *
                    """)
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var message in claimed.OrderBy(message => message.OccurredAtUtc))
        {
            await ProcessAsync(message, cancellationToken).ConfigureAwait(false);
        }

        return claimed.Count;
    }

    private async Task ProcessAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (!registry.TryResolve(message.Type, out var eventType))
        {
            await DeadLetterAsync(message, "Unknown event type.", cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var domainEvent = (IDomainEvent?)JsonSerializer.Deserialize(message.Payload, eventType, SerializerOptions)
                ?? throw new JsonException("Event payload is empty.");

            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                await DomainEventDispatcher.DispatchAsync(scope.ServiceProvider, eventType, domainEvent, cancellationToken).ConfigureAwait(false);
            }

            await UpdateAsync(message.Id, setters => setters
                .SetProperty(m => m.ProcessedAtUtc, timeProvider.GetUtcNow())
                .SetProperty(m => m.AttemptCount, message.AttemptCount + 1)
                .SetProperty(m => m.LockedBy, (string?)null)
                .SetProperty(m => m.LockedUntilUtc, (DateTimeOffset?)null), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogDeliveryFailed(logger, exception, message.Id, message.Type);
            var attempts = message.AttemptCount + 1;
            var error = Describe(exception);

            if (attempts >= options.Value.MaxOutboxAttempts)
            {
                await DeadLetterAsync(message, error, cancellationToken, attempts).ConfigureAwait(false);
                return;
            }

            var nextAttempt = timeProvider.GetUtcNow().Add(RetryBackoff.Delay(attempts, options.Value.RetryBaseDelay, options.Value.RetryMaxDelay));
            await UpdateAsync(message.Id, setters => setters
                .SetProperty(m => m.AttemptCount, attempts)
                .SetProperty(m => m.LastError, error)
                .SetProperty(m => m.NextAttemptAtUtc, nextAttempt)
                .SetProperty(m => m.LockedBy, (string?)null)
                .SetProperty(m => m.LockedUntilUtc, (DateTimeOffset?)null), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DeadLetterAsync(OutboxMessage message, string error, CancellationToken cancellationToken, int? attempts = null)
    {
        await UpdateAsync(message.Id, setters => setters
            .SetProperty(m => m.DeadLetteredAtUtc, timeProvider.GetUtcNow())
            .SetProperty(m => m.AttemptCount, attempts ?? message.AttemptCount)
            .SetProperty(m => m.LastError, error)
            .SetProperty(m => m.LockedBy, (string?)null)
            .SetProperty(m => m.LockedUntilUtc, (DateTimeOffset?)null), cancellationToken).ConfigureAwait(false);

        var shortType = message.Type[(message.Type.LastIndexOf('.') + 1)..];
        await failureNotifier.NotifyAsync(
            new NotificationRequest(
                NotificationType.OutboxMessageFailed,
                NotificationSeverity.Critical,
                Permissions.SystemJobs.View,
                new Dictionary<string, string> { ["eventType"] = shortType },
                ResourceType: "OutboxMessage",
                ResourceId: message.Id.ToString()),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fenced update: only succeeds while this instance still owns the lease.</summary>
    private async Task UpdateAsync(
        Guid messageId,
        Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<OutboxMessage>> setters,
        CancellationToken cancellationToken)
    {
        var owner = identity.InstanceId;
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.OutboxMessages
            .Where(m => m.Id == messageId && m.LockedBy == owner)
            .ExecuteUpdateAsync(setters, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string Describe(Exception exception)
    {
        var description = $"{exception.GetType().Name}: {exception.Message}";
        return description.Length > OutboxMessage.LastErrorMaxLength ? description[..OutboxMessage.LastErrorMaxLength] : description;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} ({EventType}) failed")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, Guid messageId, string eventType);
}
