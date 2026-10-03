using Microsoft.EntityFrameworkCore;
using Onyx.API.IntegrationTests.Persistence;
using Onyx.API.IntegrationTests.TestDatabase;
using Onyx.Infrastructure.Persistence;

[assembly: AssemblyFixture(typeof(Onyx.API.IntegrationTests.Infrastructure.PostgresFixture))]

namespace Onyx.API.IntegrationTests.Infrastructure;

/// <summary>
/// One pair of disposable databases per test run: the application database (real migrations) and a scratch database for
/// test-only models. Not configured → database tests are skipped. Misconfigured (unsafe) → the run fails loudly.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public ITestDatabase? ApplicationDatabase { get; private set; }

    public ITestDatabase? ScratchDatabase { get; private set; }

    public bool IsAvailable => ApplicationDatabase is not null;

    public string UnavailableReason { get; private set; } =
        $"{PostgresTestDatabaseGuard.EnvironmentVariable} is not set; database tests skipped (see docs/development-setup.md).";

    public async ValueTask InitializeAsync()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgresTestDatabaseGuard.EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;

        ApplicationDatabase = await LocalPostgresTestDatabase.CreateAsync(adminConnectionString, cancellationToken);
        await using (var applicationContext = CreateApplicationContext(ApplicationDatabase.ConnectionString))
        {
            await applicationContext.Database.MigrateAsync(cancellationToken);
        }

        ScratchDatabase = await LocalPostgresTestDatabase.CreateAsync(adminConnectionString, cancellationToken);
        await using (var scratchContext = InterceptorTestDbContext.CreateWithoutInterceptors(ScratchDatabase.ConnectionString))
        {
            // Test-only model in its own throwaway database; the application schema is only ever created by migrations.
            await scratchContext.Database.EnsureCreatedAsync(cancellationToken);
        }
    }

    public void RequireDatabase() => Assert.SkipUnless(IsAvailable, UnavailableReason);

    public static ApplicationDbContext CreateApplicationContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(ApplicationDbContext.MigrationsHistoryTable))
            .Options);

    public async ValueTask DisposeAsync()
    {
        if (ScratchDatabase is not null)
        {
            await ScratchDatabase.DisposeAsync();
        }

        if (ApplicationDatabase is not null)
        {
            await ApplicationDatabase.DisposeAsync();
        }
    }
}
