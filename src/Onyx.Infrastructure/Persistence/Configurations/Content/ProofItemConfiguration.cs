using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Content;
using Onyx.Domain.Proof;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ProofItemConfiguration : IEntityTypeConfiguration<ProofItem>
{
    public void Configure(EntityTypeBuilder<ProofItem> builder)
    {
        builder.ToTable("ProofItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.Texts).HasJsonColumn().IsRequired();
        builder.Property(item => item.Verification!).HasJsonColumn();
        builder.Property(item => item.Value).HasMaxLength(ProofItem.ValueMaxLength);
        builder.Property(item => item.AttributionName).HasMaxLength(ProofItem.AttributionMaxLength);
        builder.Property(item => item.AttributionRole).HasMaxLength(ProofItem.AttributionMaxLength);
        builder.Property(item => item.AttributionOrganization).HasMaxLength(ProofItem.AttributionMaxLength);
        builder.Property(item => item.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(item => item.ContentItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.ContentItemId, item.Status, item.SortOrder });
    }
}
