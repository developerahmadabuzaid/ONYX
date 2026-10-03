using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Npgsql;

namespace Onyx.API.IntegrationTests.TestDatabase;

/// <summary>
/// Every safety rule for test databases (docs/architecture.md §3.1). Any violation throws before a single command touches the server.
/// Defence in depth: even if these checks were bypassed, the required NOSUPERUSER test role cannot drop databases it does not own.
/// </summary>
public static partial class PostgresTestDatabaseGuard
{
    public const string EnvironmentVariable = "ONYX_TEST_POSTGRES";
    public const string MaintenanceDatabase = "postgres";
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    private const string NameAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly ConcurrentDictionary<string, byte> CreatedThisRun = new(StringComparer.Ordinal);

    /// <summary>The admin connection string must target the maintenance database and nothing else.</summary>
    public static NpgsqlConnectionStringBuilder ValidateAdminConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new TestDatabaseSafetyException($"{EnvironmentVariable} is not set.");
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new TestDatabaseSafetyException($"{EnvironmentVariable} is not a valid PostgreSQL connection string.", exception);
        }

        if (!string.Equals(builder.Database, MaintenanceDatabase, StringComparison.Ordinal))
        {
            throw new TestDatabaseSafetyException(
                $"{EnvironmentVariable} must use Database={MaintenanceDatabase}. Test databases are created per run; " +
                "an existing database (development, production or any other) is never targeted.");
        }

        return builder;
    }

    public static void EnsureSafeRole(string roleName, bool isSuperuser, bool canCreateDatabases)
    {
        if (isSuperuser)
        {
            throw new TestDatabaseSafetyException(
                $"Role '{roleName}' is a superuser. Use a dedicated NOSUPERUSER CREATEDB test role (see docs/development-setup.md).");
        }

        if (!canCreateDatabases)
        {
            throw new TestDatabaseSafetyException($"Role '{roleName}' lacks CREATEDB.");
        }
    }

    public static async Task EnsureSafeRoleAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using var command = new NpgsqlCommand(
            "SELECT current_user, rolsuper, rolcreatedb FROM pg_roles WHERE rolname = current_user", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new TestDatabaseSafetyException("Could not read the current role's attributes.");
        }

        EnsureSafeRole(reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2));
    }

    public static bool IsTestDatabaseName(string? name) => name is not null && TestDatabaseName().IsMatch(name);

    public static string NewDatabaseName(DateTimeOffset utcNow) =>
        $"onyx_test_{utcNow.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}_{RandomNumberGenerator.GetString(NameAlphabet, 8)}";

    public static void RegisterCreated(string name)
    {
        EnsureTestDatabaseName(name);
        CreatedThisRun[name] = 0;
    }

    public static void Unregister(string name) => CreatedThisRun.TryRemove(name, out _);

    /// <summary>Required before any reset, migration or drop.</summary>
    public static void EnsureOwnedByThisRun(string name)
    {
        EnsureTestDatabaseName(name);
        if (!CreatedThisRun.ContainsKey(name))
        {
            throw new TestDatabaseSafetyException($"Database '{name}' was not created by this test run.");
        }
    }

    public static bool IsStale(string name, DateTimeOffset utcNow)
    {
        var match = TestDatabaseName().Match(name);
        if (!match.Success)
        {
            return false;
        }

        var createdAt = DateTime.ParseExact(match.Groups["stamp"].Value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        return utcNow.UtcDateTime - createdAt > StaleAfter;
    }

    public static string QuoteIdentifier(string name)
    {
        EnsureTestDatabaseName(name);
        return $"\"{name}\"";
    }

    private static void EnsureTestDatabaseName(string name)
    {
        if (!IsTestDatabaseName(name))
        {
            throw new TestDatabaseSafetyException($"'{name}' is not a test database name.");
        }
    }

    [GeneratedRegex(@"^onyx_test_(?<stamp>\d{14})_[a-z0-9]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex TestDatabaseName();
}
