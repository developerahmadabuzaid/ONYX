using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Onyx.Application.Email;
using Onyx.Application.Jobs;
using Onyx.Application.Notifications;
using Onyx.Application.Providers;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Notifications;
using Onyx.Domain.Providers;
using Onyx.Infrastructure.Email;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// Sends queued emails. Unconfigured transport: messages stay queued. Transport outage: the whole batch is postponed without
/// consuming attempts (no dead-letter storm). Message-specific rejection: retried with backoff, then dead-lettered.
/// </summary>
public sealed partial class EmailProcessor(
    IServiceScopeFactory scopeFactory,
    IEmailSender sender,
    FailureNotifier failureNotifier,
    WorkerIdentity identity,
    IOptions<WorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailProcessor> logger)
{
    private int _notConfiguredReported;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        if (!sender.IsConfigured)
        {
            if (Interlocked.Exchange(ref _notConfiguredReported, 1) == 0)
            {
                await ReportProviderAsync(service => service.ReportNotConfiguredAsync(sender.ProviderKey, ProviderCategory.Email, cancellationToken)).ConfigureAwait(false);
            }

            return 0;
        }

        var claimed = await ClaimAsync(cancellationToken).ConfigureAwait(false);
        var anySent = false;

        for (var index = 0; index < claimed.Count; index++)
        {
            var message = claimed[index];
            try
            {
                await sender.SendAsync(ToEnvelope(message), cancellationToken).ConfigureAwait(false);
                await UpdateAsync([message.Id], setters => setters
                    .SetProperty(m => m.Status, EmailMessageStatus.Sent)
                    .SetProperty(m => m.SentAtUtc, timeProvider.GetUtcNow())
                    .SetProperty(m => m.AttemptCount, message.AttemptCount + 1)
                    .SetProperty(m => m.LastError, (string?)null)
                    .SetProperty(m => m.LockedBy, (string?)null)
                    .SetProperty(m => m.LockedUntilUtc, (DateTimeOffset?)null), cancellationToken).ConfigureAwait(false);
                anySent = true;
            }
            catch (EmailTransportException exception) when (exception.Kind == EmailFailureKind.ProviderUnavailable)
            {
                LogProviderUnavailable(logger, exception, sender.ProviderKey);
                var postponeUntil = timeProvider.GetUtcNow().Add(options.Value.RetryBaseDelay);
                var remaining = claimed.Skip(index).Select(m => m.Id).ToList();
                await UpdateAsync(remaining, setters => setters
                    .SetProperty(m => m.NextAttemptAtUtc, postponeUntil)
                    .SetProperty(m => m.LastError, exception.Message)
                    .SetProperty(m => m.LockedBy, (string?)null)
                    .SetProperty(m => m.LockedUntilUtc, (DateTimeOffset?)null), CancellationToken.None).ConfigureAwait(false);

                await ReportProviderAsync(service => service.ReportFailureAsync(sender.ProviderKey, ProviderCategory.Email, exception.Message, CancellationToken.None)).ConfigureAwait(false);
                return claimed.Count;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await HandleMessageFailureAsync(message, exception, cancellationToken).ConfigureAwait(false);
            }
        }

        if (anySent)
        {
            await ReportProviderAsync(service => service.ReportSuccessAsync(sender.ProviderKey, ProviderCategory.Email, CancellationToken.None)).ConfigureAwait(false);
        }

        return claimed.Count;
    }

    private async Task<List<EmailMessage>> ClaimAsync(CancellationToken cancellationToken)
    {
        var owner = identity.InstanceId;
        var now = timeProvider.GetUtcNow();
        var leaseUntil = now.Add(options.Value.Lease);
        var batchSize = options.Value.EmailBatchSize;

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.EmailMessages
            .FromSql($"""
                UPDATE email_messages
                SET locked_by = {owner}, locked_until_utc = {leaseUntil}
                WHERE id IN (
                    SELECT id FROM email_messages
                    WHERE status = 'Pending'
                      AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= {now})
                      AND (locked_until_utc IS NULL OR locked_until_utc < {now})
                    ORDER BY created_at_utc
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task HandleMessageFailureAsync(EmailMessage message, Exception exception, CancellationToken cancellationToken)
    {
        // Only our own safe descriptions are stored; raw provider exceptions may echo addresses or server details.
        var error = exception is EmailTransportException transport ? transport.Message : $"Unexpected {exception.GetType().Name}.";
        LogMessageFailed(logger, exception, message.Id);
        var attempts = message.AttemptCount + 1;

        if (attempts >= options.Value.MaxEmailAttempts)
        {
            await UpdateAsync([message.Id], setters => setters
                .SetProperty(m => m.Status, EmailMessageStatus.DeadLettered)
                .SetProperty(m => m.AttemptCount, attempts)
                .SetProperty(m => m.LastError, error)
                .SetProperty(m => m.LockedBy, (string?)null)
                .SetProperty(m => m.LockedUntilUtc, (DateTimeOffset?)null), cancellationToken).ConfigureAwait(false);

            await failureNotifier.NotifyAsync(
                new NotificationRequest(
                    NotificationType.EmailDeliveryFailed,
                    NotificationSeverity.Warning,
                    Permissions.SystemJobs.View,
                    new Dictionary<string, string> { ["template"] = message.TemplateKey },
                    ResourceType: "EmailMessage",
                    ResourceId: message.Id.ToString()),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var nextAttempt = timeProvider.GetUtcNow().Add(RetryBackoff.Delay(attempts, options.Value.RetryBaseDelay, options.Value.RetryMaxDelay));
        await UpdateAsync([message.Id], setters => setters
            .SetProperty(m => m.AttemptCount, attempts)
            .SetProperty(m => m.LastError, error)
            .SetProperty(m => m.NextAttemptAtUtc, nextAttempt)
            .SetProperty(m => m.LockedBy, (string?)null)
            .SetProperty(m => m.LockedUntilUtc, (DateTimeOffset?)null), cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAsync(IReadOnlyCollection<Guid> messageIds, Action<UpdateSettersBuilder<EmailMessage>> setters, CancellationToken cancellationToken)
    {
        if (messageIds.Count == 0)
        {
            return;
        }

        var owner = identity.InstanceId;
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.EmailMessages
            .Where(m => messageIds.Contains(m.Id) && m.LockedBy == owner)
            .ExecuteUpdateAsync(setters, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ReportProviderAsync(Func<IProviderConnectionService, Task> report)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await report(scope.ServiceProvider.GetRequiredService<IProviderConnectionService>()).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Status reporting is best effort (e.g. two Workers updating the same row concurrently); sending must not depend on it.
            LogProviderReportFailed(logger, exception, sender.ProviderKey);
        }
    }

    private static EmailEnvelope ToEnvelope(EmailMessage message) =>
        new(message.Id, message.ToAddress, message.ToName, message.Subject, message.HtmlBody, message.TextBody, message.LanguageCode, message.Attachments);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email transport {ProviderKey} unavailable; batch postponed")]
    private static partial void LogProviderUnavailable(ILogger logger, Exception exception, string providerKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email message {MessageId} failed")]
    private static partial void LogMessageFailed(ILogger logger, Exception exception, Guid messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not record status for provider {ProviderKey}")]
    private static partial void LogProviderReportFailed(ILogger logger, Exception exception, string providerKey);
}
