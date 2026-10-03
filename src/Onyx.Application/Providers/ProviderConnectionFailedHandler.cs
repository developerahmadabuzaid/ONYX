using Onyx.Application.Abstractions.Messaging;
using Onyx.Application.Notifications;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Notifications;
using Onyx.Domain.Providers;

namespace Onyx.Application.Providers;

/// <summary>Tells administrators who manage provider connections that a provider stopped working.</summary>
public sealed class ProviderConnectionFailedHandler(INotificationService notifications) : IDomainEventHandler<ProviderConnectionFailed>
{
    public async Task HandleAsync(ProviderConnectionFailed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await notifications.PublishAsync(
            new NotificationRequest(
                NotificationType.IntegrationFailed,
                NotificationSeverity.Critical,
                Permissions.ProviderConnections.Manage,
                new Dictionary<string, string> { ["provider"] = domainEvent.ProviderKey },
                ResourceType: "ProviderConnection",
                ResourceId: domainEvent.ProviderConnectionId.ToString()),
            cancellationToken).ConfigureAwait(false);
    }
}
