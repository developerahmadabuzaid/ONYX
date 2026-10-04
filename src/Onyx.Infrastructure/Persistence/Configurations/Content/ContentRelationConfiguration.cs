using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ContentRelationConfiguration : IEntityTypeConfiguration<ContentRelation>
{
    public void Configure(EntityTypeBuilder<ContentRelation> builder)
    {
        builder.ToTable("ContentRelations");
        builder.HasKey(relation => relation.Id);
        builder.Property(relation => relation.Id).ValueGeneratedNever();
        builder.Property(relation => relation.Kind).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(relation => relation.Note).HasMaxLength(ContentRelation.NoteMaxLength);

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(relation => relation.FromContentItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ContentItem>().WithMany().HasForeignKey(relation => relation.ToContentItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(relation => new { relation.FromContentItemId, relation.ToContentItemId, relation.Kind }).IsUnique();
        builder.HasIndex(relation => relation.ToContentItemId);
    }
}
