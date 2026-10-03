namespace Onyx.API.IntegrationTests.TestDatabase;

/// <summary>Pure tests of the safety rules: they run on every machine, with or without PostgreSQL.</summary>
public sealed class PostgresTestDatabaseGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_connection_string_is_rejected(string? connectionString)
    {
        Assert.Throws<TestDatabaseSafetyException>(() => PostgresTestDatabaseGuard.ValidateAdminConnectionString(connectionString));
    }

    [Theory]
    [InlineData("Host=localhost;Username=onyx_test;Password=x")]
    [InlineData("Host=localhost;Database=onyx_dev;Username=onyx_test;Password=x")]
    [InlineData("Host=localhost;Database=onyx_prod;Username=onyx_test;Password=x")]
    [InlineData("Host=localhost;Database=Postgres;Username=onyx_test;Password=x")]
    [InlineData("Host=localhost;Database=onyx_test_20261003120000_abcdefgh;Username=onyx_test;Password=x")]
    public void Connection_strings_naming_any_database_but_postgres_are_rejected(string connectionString)
    {
        Assert.Throws<TestDatabaseSafetyException>(() => PostgresTestDatabaseGuard.ValidateAdminConnectionString(connectionString));
    }

    [Fact]
    public void Maintenance_database_connection_string_is_accepted()
    {
        var builder = PostgresTestDatabaseGuard.ValidateAdminConnectionString("Host=localhost;Database=postgres;Username=onyx_test;Password=x");

        Assert.Equal("postgres", builder.Database);
    }

    [Fact]
    public void Superuser_roles_are_rejected()
    {
        var exception = Assert.Throws<TestDatabaseSafetyException>(() => PostgresTestDatabaseGuard.EnsureSafeRole("postgres", isSuperuser: true, canCreateDatabases: true));

        Assert.Contains("superuser", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Roles_without_createdb_are_rejected()
    {
        Assert.Throws<TestDatabaseSafetyException>(() => PostgresTestDatabaseGuard.EnsureSafeRole("onyx_app", isSuperuser: false, canCreateDatabases: false));
    }

    [Fact]
    public void Dedicated_test_role_is_accepted()
    {
        PostgresTestDatabaseGuard.EnsureSafeRole("onyx_test", isSuperuser: false, canCreateDatabases: true);
    }

    [Fact]
    public void Generated_names_match_the_test_pattern_and_are_unique()
    {
        var first = PostgresTestDatabaseGuard.NewDatabaseName(Now);
        var second = PostgresTestDatabaseGuard.NewDatabaseName(Now);

        Assert.True(PostgresTestDatabaseGuard.IsTestDatabaseName(first));
        Assert.StartsWith("onyx_test_20261003120000_", first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("onyx_dev")]
    [InlineData("postgres")]
    [InlineData("onyx_test")]
    [InlineData("onyx_test_2026100312000_abcdefgh")]
    [InlineData("onyx_test_20261003120000_ABCDEFGH")]
    [InlineData("onyx_test_20261003120000_abcdefgh; DROP DATABASE onyx_dev")]
    [InlineData("\"onyx_test_20261003120000_abcdefgh\"")]
    public void Non_test_names_are_never_accepted(string name)
    {
        Assert.False(PostgresTestDatabaseGuard.IsTestDatabaseName(name));
        Assert.Throws<TestDatabaseSafetyException>(() => PostgresTestDatabaseGuard.QuoteIdentifier(name));
    }

    [Fact]
    public void Databases_not_created_by_this_run_cannot_be_reset_or_dropped()
    {
        // Well-formed name, but never registered by this process.
        var foreign = PostgresTestDatabaseGuard.NewDatabaseName(Now);

        Assert.Throws<TestDatabaseSafetyException>(() => PostgresTestDatabaseGuard.EnsureOwnedByThisRun(foreign));
    }

    [Fact]
    public void Registered_databases_are_owned_until_unregistered()
    {
        var name = PostgresTestDatabaseGuard.NewDatabaseName(Now);

        PostgresTestDatabaseGuard.RegisterCreated(name);
        PostgresTestDatabaseGuard.EnsureOwnedByThisRun(name);
        PostgresTestDatabaseGuard.Unregister(name);

        Assert.Throws<TestDatabaseSafetyException>(() => PostgresTestDatabaseGuard.EnsureOwnedByThisRun(name));
    }

    [Theory]
    [InlineData("onyx_test_20261002115959_abcdefgh", true)]
    [InlineData("onyx_test_20261002120001_abcdefgh", false)]
    [InlineData("onyx_test_20261003115000_abcdefgh", false)]
    [InlineData("onyx_dev", false)]
    public void Only_test_databases_older_than_24_hours_are_stale(string name, bool expected)
    {
        Assert.Equal(expected, PostgresTestDatabaseGuard.IsStale(name, Now));
    }
}
