using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Notifications;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;
using Onyx.Domain.Notifications;

namespace Onyx.Application.Content;

public sealed class ContentSchedulingService(
    IContentRepository content,
    ContentPublisher publisher,
    INotificationService notifications,
    ContentSettings settings,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IContentSchedulingService
{
    public Task<IReadOnlyList<Guid>> ListDuePublicationsAsync(CancellationToken cancellationToken) =>
        content.ListDueForPublishAsync(timeProvider.GetUtcNow(), settings.SchedulerBatchSize, cancellationToken);

    public Task<IReadOnlyList<Guid>> ListDueUnpublicationsAsync(CancellationToken cancellationToken) =>
        content.ListDueForUnpublishAsync(timeProvider.GetUtcNow(), settings.SchedulerBatchSize, cancellationToken);

    public async Task<Result> PublishDueAsync(Guid contentItemId, CancellationToken cancellationToken)
    {
        var item = await content.GetAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        if (item is not { Status: ContentStatus.Scheduled } || item.ScheduledPublishAtUtc > timeProvider.GetUtcNow())
        {
            return Result.Success(); // Already handled by another run, or rescheduled: idempotent no-op.
        }

        var published = await publisher.PublishAsync(item, scheduled: true, cancellationToken).ConfigureAwait(false);
        if (published.IsFailure)
        {
            return published;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> UnpublishDueAsync(Guid contentItemId, CancellationToken cancellationToken)
    {
        var item = await content.GetAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        if (item is null || item.ScheduledUnpublishAtUtc is null || item.ScheduledUnpublishAtUtc > timeProvider.GetUtcNow() || !item.IsLive)
        {
            return Result.Success();
        }

        var offline = await publisher.TakeOfflineAsync(item, OfflineResolution.PendingDecision, null, archive: false, scheduled: true, cancellationToken)
            .ConfigureAwait(false);
        if (offline.IsFailure)
        {
            return offline;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task HandleFailedPublicationAsync(Guid contentItemId, string reason, CancellationToken cancellationToken)
    {
        var item = await content.GetAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return;
        }

        item.FailSchedule();
        auditTrail.Record(AuditAction.ContentScheduleCancelled, AuditCategory.Publishing, nameof(ContentItem), item.Id.ToString(),
            new { reason = reason.Length > 300 ? reason[..300] : reason, automatic = true });

        // The notification service commits the unit of work (schedule reset + audit + notification together).
        await notifications.PublishAsync(
            new NotificationRequest(NotificationType.ContentPublishingFailed, NotificationSeverity.Warning, Permissions.Content.Publish,
                new Dictionary<string, string> { ["contentItemId"] = item.Id.ToString() }, nameof(ContentItem), item.Id.ToString()),
            cancellationToken).ConfigureAwait(false);
    }
}
