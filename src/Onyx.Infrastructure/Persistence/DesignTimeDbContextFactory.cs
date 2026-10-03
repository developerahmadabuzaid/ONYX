using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Onyx.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> to build the model for migrations. The connection string carries no credentials and is never
/// opened when adding migrations; to apply migrations, pass <c>--connection</c> explicitly (see docs/development-setup.md).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=onyx_design_time", npgsql => npgsql.MigrationsHistoryTable(ApplicationDbContext.MigrationsHistoryTable))
            .Options;

        return new ApplicationDbContext(options);
    }
}
