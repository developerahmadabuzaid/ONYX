using Onyx.Domain.Common;

namespace Onyx.Domain.Tests.Common;

public sealed class EntityTests
{
    [Fact]
    public void New_entities_get_distinct_version_7_ids()
    {
        var first = new SampleEntity();
        var second = new SampleEntity();

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.Equal(7, first.Id.Version);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Raised_events_are_collected_until_cleared()
    {
        var entity = new SampleEntity();

        entity.DoSomething();
        entity.DoSomething();

        Assert.Equal(2, entity.DomainEvents.Count);

        entity.ClearDomainEvents();

        Assert.Empty(entity.DomainEvents);
    }

    private sealed record SomethingHappened(Guid EntityId) : IDomainEvent;

    private sealed class SampleEntity : Entity
    {
        public void DoSomething() => RaiseDomainEvent(new SomethingHappened(Id));
    }
}
