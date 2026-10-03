using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Onyx.Infrastructure.Persistence;

/// <summary>Readiness probe for PostgreSQL. Reports only healthy/unhealthy; never connection details.</summary>
public sealed class DatabaseHealthCheck(ApplicationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);

        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Database unavailable.");
    }
}
