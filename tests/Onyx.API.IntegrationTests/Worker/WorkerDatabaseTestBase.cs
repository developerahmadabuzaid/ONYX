using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.Persistence;
using Onyx.Domain.Common;
using Onyx.Infrastructure.Persistence;
using Onyx.Infrastructure.Persistence.Outbox;

namespace Onyx.API.IntegrationTests.Worker;

/// <summary>Resets the application test database before each test and offers seeding/reading helpers.</summary>
[Collection(DatabaseCollection.Name)]
public abstract class WorkerDatabaseTestBase(PostgresFixture fixture) : IAsyncLifetime
{
    protected static readonly DateTimeOffset Start = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    protected PostgresFixture Fixture { get; } = fixture;

    protected ManualTimeProvider Time { get; } = new(Start);

    protected string ConnectionString => Fixture.ApplicationDatabase!.ConnectionString;

    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        if (Fixture.IsAvailable)
        {
            await Fixture.ApplicationDatabase!.ResetAsync(Token);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected ApplicationDbContext CreateContext() => PostgresFixture.CreateApplicationContext(ConnectionString);

    protected async Task SeedOutboxAsync(params IDomainEvent[] events)
    {
        await using var context = CreateContext();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var domainEvent in events)
        {
            context.OutboxMessages.Add(OutboxMessage.FromDomainEvent(domainEvent, Time.GetUtcNow(), options));
        }

        await context.SaveChangesAsync(Token);
    }

    protected async Task<List<T>> ReadAsync<T>(Func<ApplicationDbContext, IQueryable<T>> query)
    {
        await using var context = CreateContext();
        return await query(context).ToListAsync(Token);
    }
}
