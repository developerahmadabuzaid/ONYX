using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ContentPathConfiguration : IEntityTypeConfiguration<ContentPath>
{
    public void Configure(EntityTypeBuilder<ContentPath> builder)
    {
        builder.ToTable("ContentPaths");
        builder.HasKey(path => path.Id);
        builder.Property(path => path.Id).ValueGeneratedNever();

        builder.Property(path => path.LanguageCode).HasMaxLength(ContentTranslation.LanguageCodeMaxLength).IsRequired();
        builder.Property(path => path.Path).HasMaxLength(ContentPath.PathMaxLength).IsRequired();

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(path => path.ContentItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(path => new { path.LanguageCode, path.Path }).IsUnique();
        builder.HasIndex(path => new { path.ContentItemId, path.LanguageCode }).IsUnique().HasFilter("is_current");
    }
}
