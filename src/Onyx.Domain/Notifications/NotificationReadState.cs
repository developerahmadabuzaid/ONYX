namespace Onyx.Domain.Notifications;

/// <summary>Per-user read marker. Absence means unread.</summary>
public sealed class NotificationReadState
{
    private NotificationReadState()
    {
    }

    public Guid NotificationId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset ReadAtUtc { get; private set; }

    public static NotificationReadState Create(Guid notificationId, Guid userId, DateTimeOffset readAtUtc)
    {
        if (notificationId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Notification and user ids are required.");
        }

        return new NotificationReadState { NotificationId = notificationId, UserId = userId, ReadAtUtc = readAtUtc };
    }
}
