using System.Collections.Concurrent;

namespace Onyx.API.IntegrationTests.Worker;

/// <summary>Shared between simulated Worker instances to prove each event is handled exactly once.</summary>
public sealed class TestEventRecorder
{
    public ConcurrentQueue<Guid> Handled { get; } = new();
}
