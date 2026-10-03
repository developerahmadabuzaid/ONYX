using Onyx.Domain.Common;

namespace Onyx.Application.Abstractions.Messaging;

/// <summary>
/// Handles a domain event delivered from the outbox by Onyx.Worker. Delivery is at-least-once: handlers must be idempotent.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
