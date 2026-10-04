using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ContentCalendarEntryConfiguration : IEntityTypeConfiguration<ContentCalendarEntry>
{
    public void Configure(EntityTypeBuilder<ContentCalendarEntry> builder)
    {
        builder.ToTable("ContentCalendarEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Topic).HasMaxLength(ContentCalendarEntry.TopicMaxLength).IsRequired();
        builder.Property(entry => entry.LanguageCode).HasMaxLength(ContentTranslation.LanguageCodeMaxLength).IsRequired();
        builder.Property(entry => entry.TargetMarket).HasMaxLength(2);
        builder.Property(entry => entry.Keyword).HasMaxLength(KeywordTarget.KeywordMaxLength);
        builder.Property(entry => entry.SearchIntent).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(entry => entry.Reason).HasMaxLength(ContentCalendarEntry.ReasonMaxLength);
        builder.Property(entry => entry.SupportingContentIds).HasJsonColumn().IsRequired();
        builder.Property(entry => entry.TimingBasis).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(entry => entry.Evidence).HasJsonColumn().IsRequired();
        builder.Property(entry => entry.Origin).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.DecisionNote).HasMaxLength(ContentCalendarEntry.NoteMaxLength);
        builder.Property(entry => entry.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(entry => entry.TargetServiceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ContentItem>().WithMany().HasForeignKey(entry => entry.LinkedContentItemId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ContentCluster>().WithMany().HasForeignKey(entry => entry.ClusterId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(entry => new { entry.Status, entry.ProposedPublishAtUtc });
    }
}
