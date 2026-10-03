using System.Reflection;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>Assemblies whose domain event types may be deserialized from the outbox (the Domain assembly by default).</summary>
public sealed class DomainEventRegistryOptions
{
    public IList<Assembly> Assemblies { get; } = [];
}
