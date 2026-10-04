using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class KeywordTargetConfiguration : IEntityTypeConfiguration<KeywordTarget>
{
    public void Configure(EntityTypeBuilder<KeywordTarget> builder)
    {
        builder.ToTable("KeywordTargets");
        builder.HasKey(target => target.Id);
        builder.Property(target => target.Id).ValueGeneratedNever();
        builder.Property(target => target.Keyword).HasMaxLength(KeywordTarget.KeywordMaxLength).IsRequired();
        builder.Property(target => target.NormalizedKeyword).HasMaxLength(KeywordTarget.KeywordMaxLength).IsRequired();
        builder.Property(target => target.LanguageCode).HasMaxLength(ContentTranslation.LanguageCodeMaxLength).IsRequired();
        builder.Property(target => target.TargetMarket).HasMaxLength(2).IsRequired();
        builder.Property(target => target.SearchIntent).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(target => target.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(target => target.Notes).HasMaxLength(KeywordTarget.NotesMaxLength);
        builder.Property(target => target.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(target => target.TargetContentItemId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ContentCluster>().WithMany().HasForeignKey(target => target.ClusterId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(target => new { target.LanguageCode, target.NormalizedKeyword, target.TargetMarket }).IsUnique();
        builder.HasIndex(target => target.TargetContentItemId);
    }
}
