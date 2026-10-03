namespace Onyx.API.IntegrationTests.Worker;

/// <summary>Shared control/observation point for <see cref="TestJob"/> across simulated Worker instances.</summary>
public sealed class TestJobProbe
{
#pragma warning disable CA1051 // Public field so Interlocked can update it atomically.
    public int Executions;
#pragma warning restore CA1051

    public TestJobMode Mode { get; set; } = TestJobMode.Succeed;

    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
}
