namespace Onyx.Domain.Common;

/// <summary>
/// Base type for all persisted entities. Ids are UUIDv7: generated in the domain (so events and audit
/// records can reference new entities before they are saved) and time-ordered for index locality.
/// Use random UUIDs separately wherever an identifier must be unguessable.
/// </summary>
public abstract class Entity : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity()
    {
        Id = Guid.CreateVersion7();
    }

    public Guid Id { get; private init; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}
