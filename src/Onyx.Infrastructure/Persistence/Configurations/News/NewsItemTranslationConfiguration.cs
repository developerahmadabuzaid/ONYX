using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Content;
using Onyx.Domain.News;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class NewsItemTranslationConfiguration : IEntityTypeConfiguration<NewsItemTranslation>
{
    public void Configure(EntityTypeBuilder<NewsItemTranslation> builder)
    {
        builder.ToTable("NewsItemTranslations");
        builder.HasKey(translation => translation.Id);
        builder.Property(translation => translation.Id).ValueGeneratedNever();
        builder.Property(translation => translation.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(translation => translation.Title).HasMaxLength(NewsItemTranslation.TitleMaxLength).IsRequired();
        builder.Property(translation => translation.Summary).HasMaxLength(NewsItemTranslation.SummaryMaxLength).IsRequired();
        builder.Property(translation => translation.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.HasIndex(translation => new { translation.NewsItemId, translation.LanguageCode }).IsUnique();
        builder.HasIndex(translation => new { translation.LanguageCode, translation.Slug }).IsUnique();
    }
}
