using System.Text.Json;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.Persistence.Outbox;

/// <summary>
/// A domain event persisted in the same transaction as the change that raised it, delivered asynchronously by Onyx.Worker.
/// Claiming uses a lease (<see cref="LockedBy"/>/<see cref="LockedUntilUtc"/>) so several Worker instances never process the same message concurrently.
/// </summary>
public sealed class OutboxMessage
{
    public const int TypeMaxLength = 500;
    public const int LockedByMaxLength = 200;
    public const int LastErrorMaxLength = 4000;

    private OutboxMessage()
    {
        Type = string.Empty;
        Payload = string.Empty;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>CLR full name of the event, resolved only against the registry of known domain event types.</summary>
    public string Type { get; private set; }

    /// <summary>Event serialized as JSON (<c>jsonb</c>).</summary>
    public string Payload { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    /// <summary>Set when retries are exhausted or the event type is unknown; such messages are never retried automatically.</summary>
    public DateTimeOffset? DeadLetteredAtUtc { get; private set; }

    public static OutboxMessage FromDomainEvent(IDomainEvent domainEvent, DateTimeOffset occurredAtUtc, JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var eventType = domainEvent.GetType();

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            OccurredAtUtc = occurredAtUtc,
            Type = eventType.FullName ?? eventType.Name,
            Payload = JsonSerializer.Serialize(domainEvent, eventType, serializerOptions),
        };
    }
}
