using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Domain.Auditing;
using Onyx.Infrastructure.Persistence.Interceptors;

namespace Onyx.API.IntegrationTests.Persistence;

/// <summary>Verifies the real interceptors against real PostgreSQL using a test-only entity in the scratch database.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class PersistenceInterceptorTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly Guid UserId = Guid.Parse("0199a1b2-0000-7000-8000-000000000001");
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync()
    {
        if (fixture.IsAvailable)
        {
            await fixture.ScratchDatabase!.ResetAsync(TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Creating_stamps_fields_audits_with_redaction_and_writes_outbox_atomically()
    {
        fixture.RequireDatabase();
        var cancellationToken = TestContext.Current.CancellationToken;
        var article = TestArticle.Create("Arabic SEO guide", apiToken: "secret-token-123", internalNote: "do not audit");

        await using (var context = CreateContext())
        {
            context.Articles.Add(article);
            await context.SaveChangesAsync(cancellationToken);
        }

        Assert.Empty(article.DomainEvents);

        await using var verify = CreateContext();
        var saved = await verify.Articles.AsNoTracking().SingleAsync(cancellationToken);
        Assert.Equal(_time.GetUtcNow(), saved.CreatedAtUtc);
        Assert.Equal(UserId, saved.CreatedById);
        Assert.Null(saved.UpdatedAtUtc);

        var log = await verify.AuditLogs.AsNoTracking().SingleAsync(cancellationToken);
        Assert.Equal(AuditAction.Created, log.Action);
        Assert.Equal(AuditCategory.Content, log.Category);
        Assert.Equal(nameof(TestArticle), log.EntityType);
        Assert.Equal(article.Id.ToString(), log.EntityId);
        Assert.Equal(UserId, log.UserId);
        Assert.Equal("auditor", log.UserName);
        Assert.Equal("203.0.113.7", log.IpAddress);
        Assert.Equal("corr-interceptor-test", log.CorrelationId);

        using var changes = JsonDocument.Parse(log.Changes!);
        Assert.Equal("Arabic SEO guide", changes.RootElement.GetProperty("Title").GetProperty("New").GetString());
        Assert.Equal(AuditLogInterceptor.RedactedValue, changes.RootElement.GetProperty("ApiToken").GetProperty("New").GetString());
        Assert.False(changes.RootElement.TryGetProperty("InternalNote", out _));
        Assert.DoesNotContain("secret-token-123", log.Changes!, StringComparison.Ordinal);

        var message = await verify.OutboxMessages.AsNoTracking().SingleAsync(cancellationToken);
        Assert.Equal(typeof(TestArticleCreated).FullName, message.Type);
        Assert.Null(message.ProcessedAtUtc);
        using var payload = JsonDocument.Parse(message.Payload);
        Assert.Equal(article.Id, payload.RootElement.GetProperty("articleId").GetGuid());
    }

    [Fact]
    public async Task Updating_records_only_real_changes_with_old_and_new_values()
    {
        fixture.RequireDatabase();
        var cancellationToken = TestContext.Current.CancellationToken;
        var article = await SeedAsync(cancellationToken);
        _time.Advance(TimeSpan.FromMinutes(5));

        await using (var context = CreateContext())
        {
            var tracked = await context.Articles.SingleAsync(entity => entity.Id == article.Id, cancellationToken);
            tracked.Rename("Arabic SEO guide (2026)");
            tracked.RotateToken("rotated-token-456");
            tracked.UpdateNote("still not audited");
            await context.SaveChangesAsync(cancellationToken);
        }

        await using var verify = CreateContext();
        var saved = await verify.Articles.AsNoTracking().SingleAsync(cancellationToken);
        Assert.Equal(_time.GetUtcNow(), saved.UpdatedAtUtc);
        Assert.Equal(UserId, saved.UpdatedById);

        var log = await verify.AuditLogs.AsNoTracking().SingleAsync(entry => entry.Action == AuditAction.Updated, cancellationToken);
        using var changes = JsonDocument.Parse(log.Changes!);
        var title = changes.RootElement.GetProperty("Title");
        Assert.Equal("Arabic SEO guide", title.GetProperty("Old").GetString());
        Assert.Equal("Arabic SEO guide (2026)", title.GetProperty("New").GetString());
        Assert.Equal(AuditLogInterceptor.RedactedValue, changes.RootElement.GetProperty("ApiToken").GetProperty("Old").GetString());
        Assert.Equal(AuditLogInterceptor.RedactedValue, changes.RootElement.GetProperty("ApiToken").GetProperty("New").GetString());
        Assert.False(changes.RootElement.TryGetProperty("InternalNote", out _));
        Assert.False(changes.RootElement.TryGetProperty("UpdatedAtUtc", out _));
    }

    [Fact]
    public async Task Saving_without_real_changes_writes_no_audit_entry()
    {
        fixture.RequireDatabase();
        var cancellationToken = TestContext.Current.CancellationToken;
        var article = await SeedAsync(cancellationToken);

        await using (var context = CreateContext())
        {
            var tracked = await context.Articles.SingleAsync(entity => entity.Id == article.Id, cancellationToken);
            tracked.Rename(tracked.Title);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using var verify = CreateContext();
        Assert.Equal(1, await verify.AuditLogs.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task Deleting_a_soft_deletable_entity_keeps_the_row_hides_it_and_audits_soft_delete()
    {
        fixture.RequireDatabase();
        var cancellationToken = TestContext.Current.CancellationToken;
        var article = await SeedAsync(cancellationToken);
        _time.Advance(TimeSpan.FromHours(1));

        await using (var context = CreateContext())
        {
            var tracked = await context.Articles.SingleAsync(entity => entity.Id == article.Id, cancellationToken);
            context.Articles.Remove(tracked);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using var verify = CreateContext();
        Assert.Empty(await verify.Articles.AsNoTracking().ToListAsync(cancellationToken));

        var row = await verify.Articles.IgnoreQueryFilters().AsNoTracking().SingleAsync(cancellationToken);
        Assert.True(row.IsDeleted);
        Assert.Equal(_time.GetUtcNow(), row.DeletedAtUtc);
        Assert.Equal(UserId, row.DeletedById);

        Assert.True(await verify.AuditLogs.AnyAsync(entry => entry.Action == AuditAction.SoftDeleted, cancellationToken));
    }

    private async Task<TestArticle> SeedAsync(CancellationToken cancellationToken)
    {
        var article = TestArticle.Create("Arabic SEO guide", "seed-token", null);
        await using var context = CreateContext();
        context.Articles.Add(article);
        await context.SaveChangesAsync(cancellationToken);
        return article;
    }

    private InterceptorTestDbContext CreateContext()
    {
        var user = new FakeCurrentUser(UserId, "auditor");
        var request = new FakeRequestContext("corr-interceptor-test", "203.0.113.7");

        return InterceptorTestDbContext.Create(
            fixture.ScratchDatabase!.ConnectionString,
            new AuditFieldsInterceptor(user, _time),
            new AuditLogInterceptor(user, request, _time),
            new DomainEventOutboxInterceptor(_time));
    }
}
