using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ContentItemConfiguration : IEntityTypeConfiguration<ContentItem>
{
    public void Configure(EntityTypeBuilder<ContentItem> builder)
    {
        builder.ToTable("ContentItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();

        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.Key).HasMaxLength(ContentItem.KeyMaxLength);
        builder.Property(item => item.Presentation).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.ReviewNote).HasMaxLength(ContentItem.ReviewNoteMaxLength);
        builder.Property(item => item.ScheduleTimeZone).HasMaxLength(ContentItem.TimeZoneMaxLength);
        builder.Property(item => item.OfflineResolution).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.InsightKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Origin).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.ProofVerification!).HasJsonColumn();
        builder.Property(item => item.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(item => item.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ContentItem>().WithMany().HasForeignKey(item => item.OfflineRedirectToId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CallToAction>().WithMany().HasForeignKey(item => item.PrimaryCtaId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(item => item.FeaturedImageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Insights.Author>().WithMany().HasForeignKey(item => item.AuthorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(item => item.Translations).WithOne().HasForeignKey(translation => translation.ContentItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Translations).HasField("_translations").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.Key).IsUnique().HasFilter("key IS NOT NULL");
        builder.HasIndex(item => item.ParentId);
        builder.HasIndex(item => new { item.Type, item.Status });
        builder.HasIndex(item => item.LiveRevisionId).HasFilter("live_revision_id IS NOT NULL");
        builder.HasIndex(item => new { item.Status, item.ScheduledPublishAtUtc }).HasFilter("scheduled_publish_at_utc IS NOT NULL");
        builder.HasIndex(item => item.ScheduledUnpublishAtUtc).HasFilter("scheduled_unpublish_at_utc IS NOT NULL");
        builder.HasIndex(item => item.OfflineResolution).HasFilter("offline_resolution = 'PendingDecision'");
    }
}
