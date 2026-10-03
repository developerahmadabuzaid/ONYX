using Microsoft.EntityFrameworkCore;
using Npgsql;
using Onyx.API.IntegrationTests.Infrastructure;

namespace Onyx.API.IntegrationTests.TestDatabase;

[Collection(DatabaseCollection.Name)]
public sealed class DatabaseLifecycleTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Application_database_is_a_fresh_test_database_with_real_migrations_applied()
    {
        fixture.RequireDatabase();
        var database = fixture.ApplicationDatabase!;
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True(PostgresTestDatabaseGuard.IsTestDatabaseName(database.DatabaseName));

        await using var context = PostgresFixture.CreateApplicationContext(database.ConnectionString);
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);

        Assert.Contains(applied, migration => migration.EndsWith("_InitialFoundation", StringComparison.Ordinal));
        Assert.Empty(pending);
    }

    [Fact]
    public async Task Schema_uses_snake_case_and_postgres_native_types()
    {
        fixture.RequireDatabase();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var connection = new NpgsqlConnection(fixture.ApplicationDatabase!.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT table_name, column_name, data_type FROM information_schema.columns " +
            "WHERE table_schema = 'public' AND table_name IN ('audit_logs', 'outbox_messages')", connection);

        var columns = new List<(string Table, string Column, string Type)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        Assert.Contains(("audit_logs", "occurred_at_utc", "timestamp with time zone"), columns);
        Assert.Contains(("audit_logs", "changes", "jsonb"), columns);
        Assert.Contains(("audit_logs", "id", "uuid"), columns);
        Assert.Contains(("outbox_messages", "payload", "jsonb"), columns);
        Assert.All(columns, column => Assert.Equal(column.Column.ToLowerInvariant(), column.Column));
    }

    [Fact]
    public async Task Test_databases_use_utf8_with_the_builtin_unicode_locale()
    {
        fixture.RequireDatabase();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var connection = new NpgsqlConnection(fixture.ApplicationDatabase!.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT pg_encoding_to_char(encoding), datlocprovider::text, datlocale FROM pg_database WHERE datname = current_database()", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));

        Assert.Equal("UTF8", reader.GetString(0));
        Assert.Equal("b", reader.GetString(1));
        Assert.Equal("C.UTF-8", reader.GetString(2));
    }

    [Fact]
    public async Task Disposing_a_test_database_drops_it()
    {
        fixture.RequireDatabase();
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgresTestDatabaseGuard.EnvironmentVariable);

        var database = await LocalPostgresTestDatabase.CreateAsync(adminConnectionString, cancellationToken);
        var name = database.DatabaseName;
        Assert.True(await DatabaseExistsAsync(adminConnectionString!, name, cancellationToken));

        await database.DisposeAsync();

        Assert.False(await DatabaseExistsAsync(adminConnectionString!, name, cancellationToken));
        Assert.Throws<TestDatabaseSafetyException>(() => PostgresTestDatabaseGuard.EnsureOwnedByThisRun(name));
    }

    private static async Task<bool> DatabaseExistsAsync(string adminConnectionString, string name, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", connection);
        command.Parameters.AddWithValue("name", name);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
