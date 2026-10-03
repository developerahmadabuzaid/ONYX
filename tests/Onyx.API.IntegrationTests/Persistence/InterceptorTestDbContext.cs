using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Onyx.Domain.Auditing;
using Onyx.Infrastructure.Persistence;
using Onyx.Infrastructure.Persistence.Outbox;

namespace Onyx.API.IntegrationTests.Persistence;

/// <summary>Test-only context: the shared audit/outbox model and conventions plus <see cref="TestArticle"/>, in a scratch database.</summary>
public sealed class InterceptorTestDbContext(DbContextOptions<InterceptorTestDbContext> options) : DbContext(options)
{
    public DbSet<TestArticle> Articles => Set<TestArticle>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public static InterceptorTestDbContext CreateWithoutInterceptors(string connectionString) => Create(connectionString);

    public static InterceptorTestDbContext Create(string connectionString, params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<InterceptorTestDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(interceptors)
            .Options);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.ApplyOnyxConventionDefaults();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyCoreInfrastructureModel();
        modelBuilder.Entity<TestArticle>(article =>
        {
            article.ToTable("TestArticles");
            article.HasKey(entity => entity.Id);
            article.Property(entity => entity.Id).ValueGeneratedNever();
            article.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        });

        modelBuilder.ApplyOnyxConventions();
    }
}
