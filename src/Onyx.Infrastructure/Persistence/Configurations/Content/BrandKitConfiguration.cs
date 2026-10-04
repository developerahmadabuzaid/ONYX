using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class BrandKitConfiguration : IEntityTypeConfiguration<BrandKit>
{
    public void Configure(EntityTypeBuilder<BrandKit> builder)
    {
        builder.ToTable("BrandKits");
        builder.HasKey(kit => kit.Id);
        builder.Property(kit => kit.Id).ValueGeneratedNever();
        builder.Property(kit => kit.WebsiteUrl).HasMaxLength(BrandKit.UrlMaxLength);
        builder.Property(kit => kit.CompanyEmail).HasMaxLength(BrandKit.EmailMaxLength);
        builder.Property(kit => kit.Colors).HasJsonColumn().IsRequired();
        builder.Property(kit => kit.Typefaces).HasJsonColumn().IsRequired();
        builder.Property(kit => kit.ImageGenerationNotes).HasMaxLength(BrandKit.RulesNotesMaxLength);
        builder.Property(kit => kit.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(kit => kit.OfficialLogoMediaId).OnDelete(DeleteBehavior.Restrict);
    }
}
