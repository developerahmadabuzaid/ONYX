using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Domain.Auditing;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;
using Onyx.Domain.Notifications;
using Onyx.Domain.Providers;
using Onyx.Infrastructure.BackgroundJobs;
using Onyx.Infrastructure.Email;
using Onyx.Infrastructure.Identity;
using Onyx.Infrastructure.Persistence.Outbox;

namespace Onyx.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IUnitOfWork
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

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<LoginChallenge> LoginChallenges => Set<LoginChallenge>();

    public DbSet<DataProtectionKeyRecord> DataProtectionKeys => Set<DataProtectionKeyRecord>();

    public DbSet<Domain.Privacy.CookieConsentReceipt> CookieConsentReceipts => Set<Domain.Privacy.CookieConsentReceipt>();

    public DbSet<ContentItem> ContentItems => Set<ContentItem>();

    public DbSet<ContentTranslation> ContentTranslations => Set<ContentTranslation>();

    public DbSet<ContentRevision> ContentRevisions => Set<ContentRevision>();

    public DbSet<ContentPath> ContentPaths => Set<ContentPath>();

    public DbSet<Redirect> Redirects => Set<Redirect>();

    public DbSet<ContentPreviewToken> ContentPreviewTokens => Set<ContentPreviewToken>();

    public DbSet<CallToAction> CallToActions => Set<CallToAction>();

    public DbSet<CtaDefault> CtaDefaults => Set<CtaDefault>();

    public DbSet<ContentRelation> ContentRelations => Set<ContentRelation>();

    public DbSet<ContentCluster> ContentClusters => Set<ContentCluster>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<MediaUsage> MediaUsages => Set<MediaUsage>();

    public DbSet<BrandKit> BrandKits => Set<BrandKit>();

    public DbSet<KnowledgeEntry> KnowledgeEntries => Set<KnowledgeEntry>();

    public DbSet<InformationRequest> InformationRequests => Set<InformationRequest>();

    public DbSet<SeoRecommendation> SeoRecommendations => Set<SeoRecommendation>();

    public DbSet<SeoChangeObservation> SeoChangeObservations => Set<SeoChangeObservation>();

    public DbSet<KeywordTarget> KeywordTargets => Set<KeywordTarget>();

    public DbSet<CompetitorReference> CompetitorReferences => Set<CompetitorReference>();

    public DbSet<ContentPerformanceSnapshot> ContentPerformanceSnapshots => Set<ContentPerformanceSnapshot>();

    public DbSet<ContentCalendarEntry> ContentCalendarEntries => Set<ContentCalendarEntry>();

    public DbSet<EmailTemplateCopy> EmailTemplateCopies => Set<EmailTemplateCopy>();

    /// <summary>Optimistic-concurrency failures surface as <see cref="ConcurrencyConflictException"/> (409), never as a generic error.</summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("The record was changed by someone else. Reload and try again.", exception);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.ApplyOnyxConventionDefaults();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's own model first, then Onyx configurations, then conventions (snake_case) last.
        base.OnModelCreating(builder);
        builder.ApplyCoreInfrastructureModel();
        builder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly,
            type => type.Namespace?.StartsWith("Onyx.Infrastructure.Persistence.Configurations.Application", StringComparison.Ordinal) == true);

        builder.ApplyOnyxConventions();
    }
}
