using System.Text.Json;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.Persistence.Outbox;

/// <summary>
/// A domain event persisted in the same transaction as the change that raised it, for asynchronous processing by Onyx.Worker
/// (dispatching, retries and leasing arrive in Stage 2).
/// </summary>
public sealed class OutboxMessage
{
    public const int TypeMaxLength = 500;

    private OutboxMessage()
    {
        Type = string.Empty;
        Payload = string.Empty;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>CLR full name of the event; resolved against a registry of known event types when dispatching.</summary>
    public string Type { get; private set; }

    /// <summary>Event serialized as JSON (<c>jsonb</c>).</summary>
    public string Payload { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

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
