using Onyx.Domain.Common;

namespace Onyx.API.IntegrationTests.Worker;

/// <summary>Test-only event. <see cref="ShouldFail"/> makes its handler throw.</summary>
public sealed record TestOutboxEvent(Guid EventId, bool ShouldFail) : IDomainEvent;
