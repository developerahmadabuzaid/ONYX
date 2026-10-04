using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ContentRevisionConfiguration : IEntityTypeConfiguration<ContentRevision>
{
    public void Configure(EntityTypeBuilder<ContentRevision> builder)
    {
        builder.ToTable("ContentRevisions");
        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.Id).ValueGeneratedNever();

        builder.Property(revision => revision.Kind).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(revision => revision.Snapshot).HasColumnType("jsonb").IsRequired();
        builder.Property(revision => revision.ContentHash).HasMaxLength(ContentRevision.HashLength).IsFixedLength().IsRequired();
        builder.Property(revision => revision.Note).HasMaxLength(ContentRevision.NoteMaxLength);

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(revision => revision.ContentItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(revision => new { revision.ContentItemId, revision.Number }).IsUnique();
    }
}
