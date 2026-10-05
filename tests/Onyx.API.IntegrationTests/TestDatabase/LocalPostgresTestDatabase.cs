using Npgsql;
using Onyx.Infrastructure.Persistence;

namespace Onyx.API.IntegrationTests.TestDatabase;

/// <summary>Disposable database on a local PostgreSQL server, created and dropped by the test run through a dedicated NOSUPERUSER CREATEDB role.</summary>
public sealed class LocalPostgresTestDatabase : ITestDatabase
{
    private readonly string _adminConnectionString;

    private LocalPostgresTestDatabase(string adminConnectionString, string databaseName, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public static async Task<LocalPostgresTestDatabase> CreateAsync(string? adminConnectionString, CancellationToken cancellationToken)
    {
        var admin = PostgresTestDatabaseGuard.ValidateAdminConnectionString(adminConnectionString);
        var now = DateTimeOffset.UtcNow;

        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await PostgresTestDatabaseGuard.EnsureSafeRoleAsync(connection, cancellationToken);
        await DropStaleDatabasesAsync(connection, now, cancellationToken);

        var name = PostgresTestDatabaseGuard.NewDatabaseName(now);
        // Same locale settings as real Onyx databases (docs/architecture.md §18).
        await ExecuteAsync(
            connection,
            $"CREATE DATABASE {PostgresTestDatabaseGuard.QuoteIdentifier(name)} TEMPLATE template0 ENCODING 'UTF8' " +
            "LOCALE_PROVIDER builtin BUILTIN_LOCALE 'C.UTF-8' LC_COLLATE 'C' LC_CTYPE 'C'",
            cancellationToken);
        PostgresTestDatabaseGuard.RegisterCreated(name);

        var testConnectionString = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = name }.ConnectionString;
        return new LocalPostgresTestDatabase(admin.ConnectionString, name, testConnectionString);
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        PostgresTestDatabaseGuard.EnsureOwnedByThisRun(DatabaseName);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        if (!string.Equals(connection.Database, DatabaseName, StringComparison.Ordinal))
        {
            throw new TestDatabaseSafetyException($"Refusing to reset '{connection.Database}'.");
        }

        var tables = new List<string>();
        await using (var command = new NpgsqlCommand(
            "SELECT quote_ident(tablename) FROM pg_tables WHERE schemaname = 'public' AND tablename <> @history", connection))
        {
            command.Parameters.AddWithValue("history", ApplicationDbContext.MigrationsHistoryTable);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
        }

        if (tables.Count > 0)
        {
            await ExecuteAsync(connection, $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE", cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        PostgresTestDatabaseGuard.EnsureOwnedByThisRun(DatabaseName);
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {PostgresTestDatabaseGuard.QuoteIdentifier(DatabaseName)} WITH (FORCE)", CancellationToken.None);
        PostgresTestDatabaseGuard.Unregister(DatabaseName);
    }

    /// <summary>Removes databases left by crashed runs: only names matching the test pattern, owned by this role, and older than 24 hours.</summary>
    private static async Task DropStaleDatabasesAsync(NpgsqlConnection connection, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var stale = new List<string>();
        await using (var command = new NpgsqlCommand(
            "SELECT d.datname FROM pg_database d JOIN pg_roles r ON r.oid = d.datdba " +
            "WHERE r.rolname = current_user AND d.datname LIKE 'onyx\\_test\\_%'", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                if (PostgresTestDatabaseGuard.IsTestDatabaseName(name) && PostgresTestDatabaseGuard.IsStale(name, now))
                {
                    stale.Add(name);
                }
            }
        }

        foreach (var name in stale)
        {
            await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {PostgresTestDatabaseGuard.QuoteIdentifier(name)} WITH (FORCE)", cancellationToken);
        }
    }

    /// <summary>
    /// CREATE/DROP DATABASE wait for an immediate checkpoint; late in a full run (many dirty buffers) that can exceed Npgsql's
    /// default 30-second command timeout, so these administrative statements get a longer one.
    /// </summary>
    private const int DatabaseCommandTimeoutSeconds = 180;

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = DatabaseCommandTimeoutSeconds };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
