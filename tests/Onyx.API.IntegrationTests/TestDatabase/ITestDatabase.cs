namespace Onyx.API.IntegrationTests.TestDatabase;

/// <summary>
/// A disposable database owned by the current test run. Tests depend only on this abstraction so a containerized
/// implementation can replace <see cref="LocalPostgresTestDatabase"/> in CI without changing any test.
/// </summary>
public interface ITestDatabase : IAsyncDisposable
{
    string DatabaseName { get; }

    string ConnectionString { get; }

    /// <summary>Empties every application table (keeps the migrations history). Only ever runs against this run's database.</summary>
    Task ResetAsync(CancellationToken cancellationToken);
}
