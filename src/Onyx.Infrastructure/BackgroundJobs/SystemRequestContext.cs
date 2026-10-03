using Onyx.Application.Abstractions;

namespace Onyx.Infrastructure.BackgroundJobs;

public sealed class SystemRequestContext : IRequestContext
{
    public string? CorrelationId => null;

    public string? IpAddress => null;
}
