namespace Onyx.Contracts.Notifications;

/// <summary>A Notification Center item. The Admin UI localizes <see cref="Type"/> with <see cref="Parameters"/>.</summary>
public sealed record AdminNotificationResponse(
    Guid Id,
    string Type,
    string Severity,
    IReadOnlyDictionary<string, string> Parameters,
    string? ResourceType,
    string? ResourceId,
    DateTimeOffset CreatedAtUtc,
    bool IsRead);
