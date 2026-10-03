using Onyx.Application.Abstractions;

namespace Onyx.API.IntegrationTests.Persistence;

public sealed class FakeRequestContext(string? correlationId, string? ipAddress) : IRequestContext
{
    public string? CorrelationId { get; } = correlationId;

    public string? IpAddress { get; } = ipAddress;
}
