namespace Onyx.Application.Notifications;

/// <summary>The admin reading notifications, with the permissions resolved server-side for that user.</summary>
public sealed record NotificationViewer(Guid UserId, IReadOnlySet<string> Permissions);
