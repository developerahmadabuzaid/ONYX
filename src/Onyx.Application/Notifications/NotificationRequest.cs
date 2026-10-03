using Onyx.Domain.Notifications;

namespace Onyx.Application.Notifications;

public sealed record NotificationRequest(
    NotificationType Type,
    NotificationSeverity Severity,
    string RequiredPermission,
    IReadOnlyDictionary<string, string>? Parameters = null,
    string? ResourceType = null,
    string? ResourceId = null);
