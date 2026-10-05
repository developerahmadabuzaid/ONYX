namespace Onyx.Infrastructure.Persistence.Outbox;

/// <summary>
/// Marks a side-effecting event handler as done for one event (see <see cref="Application.Abstractions.Messaging.IProcessedEventLog"/>).
/// Holds no personal data; pruned with delivered outbox messages, after which the event can no longer be redelivered.
/// </summary>
public sealed class ProcessedEvent
{
    public const int KeyMaxLength = 200;

    private ProcessedEvent()
    {
        Key = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Key { get; private set; }

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    public static ProcessedEvent Create(string key, DateTimeOffset now) =>
        new() { Id = Guid.CreateVersion7(), Key = key, ProcessedAtUtc = now };
}
