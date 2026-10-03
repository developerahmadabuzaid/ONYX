namespace Onyx.Application.Abstractions;

/// <summary>Request metadata used for auditing and diagnostics. Values are empty for background work.</summary>
public interface IRequestContext
{
    string? CorrelationId { get; }

    string? IpAddress { get; }
}
