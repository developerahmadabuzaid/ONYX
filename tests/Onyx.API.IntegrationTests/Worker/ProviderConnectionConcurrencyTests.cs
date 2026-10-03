using Microsoft.EntityFrameworkCore;
using Npgsql;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Domain.Providers;

namespace Onyx.API.IntegrationTests.Worker;

/// <summary>Proves the xmin mapping: no physical version column, and concurrent edits are detected.</summary>
public sealed class ProviderConnectionConcurrencyTests(PostgresFixture fixture) : WorkerDatabaseTestBase(fixture)
{
    [Fact]
    public async Task Version_maps_to_the_xmin_system_column_not_a_physical_column()
    {
        Fixture.RequireDatabase();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(Token);
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.columns WHERE table_name = 'provider_connections' AND column_name IN ('version', 'xmin')",
            connection);

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync(Token))!);
    }

    [Fact]
    public async Task Concurrent_edits_are_rejected_with_a_concurrency_exception()
    {
        Fixture.RequireDatabase();
        await using (var seed = CreateContext())
        {
            seed.ProviderConnections.Add(ProviderConnection.Register("calendar.internal", ProviderCategory.Calendar));
            await seed.SaveChangesAsync(Token);
        }

        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstCopy = await first.ProviderConnections.SingleAsync(Token);
        var secondCopy = await second.ProviderConnections.SingleAsync(Token);

        firstCopy.RecordSuccess(Start);
        await first.SaveChangesAsync(Token);

        secondCopy.Disable();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Token));
    }
}
