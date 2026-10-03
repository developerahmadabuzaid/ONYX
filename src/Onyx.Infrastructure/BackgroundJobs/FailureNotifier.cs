using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Onyx.Application.Notifications;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>Publishes operational failure notifications in an isolated scope. A failure to notify is logged, never rethrown into the processing loop.</summary>
public sealed partial class FailureNotifier(IServiceScopeFactory scopeFactory, ILogger<FailureNotifier> logger)
{
    public async Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifications.PublishAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogNotifyFailed(logger, exception, request.Type.ToString());
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not publish {NotificationType} notification")]
    private static partial void LogNotifyFailed(ILogger logger, Exception exception, string notificationType);
}
