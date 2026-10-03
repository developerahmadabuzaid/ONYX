namespace Onyx.Domain.Common;

/// <summary>Something that happened in the domain. Persisted to the outbox in the same transaction as the change that raised it.</summary>
#pragma warning disable CA1040 // Marker interface: events are plain records identified by type.
public interface IDomainEvent;
#pragma warning restore CA1040
