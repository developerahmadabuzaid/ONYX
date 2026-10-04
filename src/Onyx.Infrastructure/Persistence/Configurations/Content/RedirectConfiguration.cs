using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class RedirectConfiguration : IEntityTypeConfiguration<Redirect>
{
    public void Configure(EntityTypeBuilder<Redirect> builder)
    {
        builder.ToTable("Redirects");
        builder.HasKey(redirect => redirect.Id);
        builder.Property(redirect => redirect.Id).ValueGeneratedNever();

        builder.Property(redirect => redirect.LanguageCode).HasMaxLength(ContentTranslation.LanguageCodeMaxLength).IsRequired();
        builder.Property(redirect => redirect.FromPath).HasMaxLength(ContentPath.PathMaxLength).IsRequired();
        builder.Property(redirect => redirect.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(redirect => redirect.TargetPath).HasMaxLength(ContentPath.PathMaxLength);
        builder.Property(redirect => redirect.Note).HasMaxLength(Redirect.NoteMaxLength);

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(redirect => redirect.TargetContentItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(redirect => new { redirect.LanguageCode, redirect.FromPath }).IsUnique();
        builder.HasIndex(redirect => redirect.TargetContentItemId).HasFilter("target_content_item_id IS NOT NULL");
    }
}
