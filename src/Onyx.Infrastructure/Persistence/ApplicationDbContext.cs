using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Domain.Auditing;
using Onyx.Domain.Brand;
using Onyx.Domain.Common;
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

    public DbSet<Domain.Proof.FeaturedProject> FeaturedProjects => Set<Domain.Proof.FeaturedProject>();

    public DbSet<Domain.Proof.ProofItem> ProofItems => Set<Domain.Proof.ProofItem>();

    public DbSet<Domain.Insights.Author> Authors => Set<Domain.Insights.Author>();

    public DbSet<EmailTemplateCopy> EmailTemplateCopies => Set<EmailTemplateCopy>();

    public DbSet<Domain.Leads.Inquiry> Inquiries => Set<Domain.Leads.Inquiry>();

    public DbSet<Domain.Leads.ConsentRecord> ConsentRecords => Set<Domain.Leads.ConsentRecord>();

    public DbSet<Domain.Leads.Lead> Leads => Set<Domain.Leads.Lead>();

    public DbSet<Domain.Leads.LeadNote> LeadNotes => Set<Domain.Leads.LeadNote>();

    public DbSet<Domain.Leads.LeadActivity> LeadActivities => Set<Domain.Leads.LeadActivity>();

    public DbSet<Domain.Booking.BookingSettings> BookingSettings => Set<Domain.Booking.BookingSettings>();

    public DbSet<Domain.Booking.AvailabilityRule> AvailabilityRules => Set<Domain.Booking.AvailabilityRule>();

    public DbSet<Domain.Booking.AvailabilityException> AvailabilityExceptions => Set<Domain.Booking.AvailabilityException>();

    public DbSet<Domain.Booking.ConsultationBooking> ConsultationBookings => Set<Domain.Booking.ConsultationBooking>();

    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    public DbSet<Domain.Privacy.DataSubjectRequest> DataSubjectRequests => Set<Domain.Privacy.DataSubjectRequest>();

    public DbSet<Domain.Privacy.RetentionPolicy> RetentionPolicies => Set<Domain.Privacy.RetentionPolicy>();

    public DbSet<Search.SearchDocument> SearchDocuments => Set<Search.SearchDocument>();

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
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23505" } unique
            && unique.ConstraintName is Configurations.Application.FeaturedProjectConfiguration.SlotIndex or Configurations.Application.FeaturedProjectConfiguration.ContentIndex)
        {
            // Two editors featured projects at the same moment; the slot table kept the limit. Report it as a conflict to retry.
            throw new ConcurrencyConflictException("Another change to the featured projects happened at the same time. Reload and try again.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: Configurations.Application.ConsultationBookingConfiguration.ConfirmedSlotIndex })
        {
            // A concurrent request confirmed the same slot first; the booking service reports it as "slot unavailable".
            throw new ConcurrencyConflictException("That time was just booked. Choose another slot.", Application.Booking.BookingService.SlotTakenCode, exception);
        }
    }

    public async Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (Database.CurrentTransaction is not null)
        {
            // Already inside a transaction: join it; the outer owner commits.
            return await work(cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var result = await work(cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            ChangeTracker.Clear();
        }

        return result;
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.ApplyOnyxConventionDefaults();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's own model first, then Onyx configurations, then conventions (snake_case) last.
        base.OnModelCreating(builder);
        // Trigram similarity for site search (typo tolerance, partial words); a trusted extension the database owner can create.
        builder.HasPostgresExtension("pg_trgm");
        builder.ApplyCoreInfrastructureModel();
        builder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly,
            type => type.Namespace?.StartsWith("Onyx.Infrastructure.Persistence.Configurations.Application", StringComparison.Ordinal) == true);

        builder.ApplyOnyxConventions();
    }
}
