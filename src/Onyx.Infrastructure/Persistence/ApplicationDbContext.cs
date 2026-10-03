using Microsoft.EntityFrameworkCore;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Domain.Auditing;
using Onyx.Domain.Notifications;
using Onyx.Domain.Providers;
using Onyx.Infrastructure.BackgroundJobs;
using Onyx.Infrastructure.Email;
using Onyx.Infrastructure.Persistence.Outbox;

namespace Onyx.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options), IUnitOfWork
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();

    public DbSet<NotificationReadState> NotificationReadStates => Set<NotificationReadState>();

    public DbSet<ProviderConnection> ProviderConnections => Set<ProviderConnection>();

    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

    public DbSet<JobDefinition> JobDefinitions => Set<JobDefinition>();

    public DbSet<JobRun> JobRuns => Set<JobRun>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.ApplyOnyxConventionDefaults();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyCoreInfrastructureModel();
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly,
            type => type.Namespace?.StartsWith("Onyx.Infrastructure.Persistence.Configurations.Application", StringComparison.Ordinal) == true);

        modelBuilder.ApplyOnyxConventions();
    }
}
