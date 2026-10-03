using Onyx.Application.Abstractions.Messaging;

namespace Onyx.API.IntegrationTests.Worker;

public sealed class TestOutboxEventHandler(TestEventRecorder recorder) : IDomainEventHandler<TestOutboxEvent>
{
    public const string FailureMessage = "Test handler failure";

    public Task HandleAsync(TestOutboxEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (domainEvent.ShouldFail)
        {
            throw new InvalidOperationException(FailureMessage);
        }

        recorder.Handled.Enqueue(domainEvent.EventId);
        return Task.CompletedTask;
    }
}
