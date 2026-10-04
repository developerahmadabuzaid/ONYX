using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("MediaAssets");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.Id).ValueGeneratedNever();

        builder.Property(asset => asset.StorageKey).HasMaxLength(MediaAsset.StorageKeyMaxLength).IsRequired();
        builder.Property(asset => asset.OriginalFileName).HasMaxLength(MediaAsset.FileNameMaxLength).IsRequired();
        builder.Property(asset => asset.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(asset => asset.ContentType).HasMaxLength(MediaAsset.ContentTypeMaxLength).IsRequired();
        builder.Property(asset => asset.Extension).HasMaxLength(10).IsRequired();
        builder.Property(asset => asset.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(asset => asset.Title).HasMaxLength(MediaAsset.TitleMaxLength);
        builder.Property(asset => asset.Texts).HasJsonColumn().IsRequired();
        builder.Property(asset => asset.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasIndex(asset => asset.StorageKey).IsUnique();
        builder.HasIndex(asset => asset.Sha256).IsUnique();
        builder.HasIndex(asset => asset.CreatedAtUtc);
    }
}
