using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Content;
using Onyx.Domain.Proof;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class FeaturedProjectConfiguration : IEntityTypeConfiguration<FeaturedProject>
{
    public const string SlotIndex = "ix_featured_projects_slot";
    public const string ContentIndex = "ix_featured_projects_content_item_id";

    public void Configure(EntityTypeBuilder<FeaturedProject> builder)
    {
        builder.ToTable("FeaturedProjects", table => table.HasCheckConstraint("ck_featured_projects_slot", $"slot BETWEEN 1 AND {FeaturedProject.MaxFeatured}"));
        builder.HasKey(featured => featured.Id);
        builder.Property(featured => featured.Id).ValueGeneratedNever();
        builder.HasOne<ContentItem>().WithMany().HasForeignKey(featured => featured.ContentItemId).OnDelete(DeleteBehavior.Cascade);

        // The limit and the one-slot-per-project rule live in the database, so concurrent requests cannot exceed them.
        builder.HasIndex(featured => featured.Slot).IsUnique().HasDatabaseName(SlotIndex);
        builder.HasIndex(featured => featured.ContentItemId).IsUnique().HasDatabaseName(ContentIndex);
    }
}
