using Microsoft.Extensions.Options;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// Allowlist of event types the outbox may deserialize. Type names stored in the database are never passed to
/// <c>Type.GetType</c>: an unknown name is dead-lettered instead of loading arbitrary types.
/// </summary>
public sealed class DomainEventTypeRegistry
{
    private readonly Dictionary<string, Type> _types;

    public DomainEventTypeRegistry(IOptions<DomainEventRegistryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _types = options.Value.Assemblies
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false, FullName: not null } && typeof(IDomainEvent).IsAssignableFrom(type))
            .ToDictionary(type => type.FullName!, StringComparer.Ordinal);
    }

    public bool TryResolve(string typeName, out Type eventType) => _types.TryGetValue(typeName, out eventType!);
}
