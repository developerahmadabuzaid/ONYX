using Onyx.Application.Abstractions.Messaging;
using Onyx.Application.Notifications;
using Onyx.Contracts.Authorization;
using Onyx.Domain.News;
using Onyx.Domain.Notifications;

namespace Onyx.Application.News;

/// <summary>Tells source administrators that a feed stopped working (once per run of failures; the source holds the error code).</summary>
public sealed class NewsSourceFailingHandler(INotificationService notifications) : IDomainEventHandler<NewsSourceFailing>
{
    public async Task HandleAsync(NewsSourceFailing domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await notifications.PublishAsync(
            new NotificationRequest(
                NotificationType.NewsIngestionFailed,
                NotificationSeverity.Warning,
                Permissions.NewsSources.Manage,
                new Dictionary<string, string> { ["source"] = domainEvent.SourceName },
                ResourceType: "NewsSource",
                ResourceId: domainEvent.NewsSourceId.ToString()),
            cancellationToken).ConfigureAwait(false);
    }
}
