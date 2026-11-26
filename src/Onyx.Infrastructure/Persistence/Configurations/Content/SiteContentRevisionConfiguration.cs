using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.SiteContent;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class SiteContentRevisionConfiguration : IEntityTypeConfiguration<SiteContentRevision>
{
    public void Configure(EntityTypeBuilder<SiteContentRevision> builder)
    {
        builder.ToTable("SiteContentRevisions");
        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.Id).ValueGeneratedNever();
        builder.Property(revision => revision.Json).HasColumnType("jsonb").IsRequired();
        builder.Property(revision => revision.MediaIds).IsRequired();
        builder.HasIndex(revision => new { revision.DocumentId, revision.Number }).IsUnique();
        builder.HasOne<SiteContentDocument>().WithMany().HasForeignKey(revision => revision.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
