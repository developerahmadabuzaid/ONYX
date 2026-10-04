using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class MediaUsageConfiguration : IEntityTypeConfiguration<MediaUsage>
{
    public void Configure(EntityTypeBuilder<MediaUsage> builder)
    {
        builder.ToTable("MediaUsages");
        builder.HasKey(usage => usage.Id);
        builder.Property(usage => usage.Id).ValueGeneratedNever();
        builder.Property(usage => usage.LanguageCode).HasMaxLength(ContentTranslation.LanguageCodeMaxLength).IsRequired();
        builder.Property(usage => usage.Context).HasConversion<string>().HasMaxLength(30).IsRequired();

        // Restrict: a file that content depends on cannot be deleted underneath it.
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(usage => usage.MediaAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ContentItem>().WithMany().HasForeignKey(usage => usage.ContentItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(usage => new { usage.MediaAssetId, usage.ContentItemId, usage.LanguageCode, usage.Context, usage.IsLive }).IsUnique();
        builder.HasIndex(usage => usage.ContentItemId);
    }
}
