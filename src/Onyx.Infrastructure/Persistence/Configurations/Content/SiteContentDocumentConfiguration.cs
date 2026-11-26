using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.SiteContent;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class SiteContentDocumentConfiguration : IEntityTypeConfiguration<SiteContentDocument>
{
    public void Configure(EntityTypeBuilder<SiteContentDocument> builder)
    {
        builder.ToTable("SiteContentDocuments");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id).ValueGeneratedNever();
        builder.Property(document => document.Key).HasMaxLength(SiteContentDocument.KeyMaxLength).IsRequired();
        builder.HasIndex(document => document.Key).IsUnique();
        builder.Property(document => document.DraftJson).HasColumnType("jsonb");
        builder.Property(document => document.PublishedJson).HasColumnType("jsonb");
        builder.Property(document => document.DraftMediaIds).IsRequired();
        builder.Property(document => document.PublishedMediaIds).IsRequired();
        builder.Property(document => document.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(document => document.HasDraft);
    }
}
