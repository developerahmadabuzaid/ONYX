using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ContentTranslationConfiguration : IEntityTypeConfiguration<ContentTranslation>
{
    public void Configure(EntityTypeBuilder<ContentTranslation> builder)
    {
        builder.ToTable("ContentTranslations");
        builder.HasKey(translation => translation.Id);
        builder.Property(translation => translation.Id).ValueGeneratedNever();
        builder.Ignore(translation => translation.Seo);
        builder.Ignore(translation => translation.IsComplete);

        builder.Property(translation => translation.LanguageCode).HasMaxLength(ContentTranslation.LanguageCodeMaxLength).IsRequired();
        builder.Property(translation => translation.Title).HasMaxLength(ContentTranslation.TitleMaxLength).IsRequired();
        builder.Property(translation => translation.Summary).HasMaxLength(ContentTranslation.SummaryMaxLength).IsRequired();
        builder.Property(translation => translation.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.Property(translation => translation.SlugScope).HasMaxLength(ContentTranslation.SlugScopeMaxLength).IsRequired();
        builder.Property(translation => translation.Body).HasJsonColumn().IsRequired();
        builder.Property(translation => translation.Faqs).HasJsonColumn().IsRequired();
        builder.Property(translation => translation.PrimaryKeyword).HasMaxLength(SeoProfile.KeywordMaxLength);
        builder.Property(translation => translation.PrimaryKeywordNormalized).HasMaxLength(SeoProfile.KeywordMaxLength);
        builder.Property(translation => translation.SecondaryKeywords).HasJsonColumn().IsRequired();
        builder.Property(translation => translation.SearchIntent).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(translation => translation.MetaTitle).HasMaxLength(SeoProfile.MetaTitleMaxLength);
        builder.Property(translation => translation.MetaDescription).HasMaxLength(SeoProfile.MetaDescriptionMaxLength);
        builder.Property(translation => translation.CanonicalPath).HasMaxLength(ContentBodyRules.MaxPathLength);
        builder.Property(translation => translation.StructuredData).HasConversion<string>().HasMaxLength(100).IsRequired();
        builder.Property(translation => translation.OpenGraphTitle).HasMaxLength(SeoProfile.OpenGraphTitleMaxLength);
        builder.Property(translation => translation.OpenGraphDescription).HasMaxLength(SeoProfile.OpenGraphDescriptionMaxLength);
        builder.Property(translation => translation.TargetMarket).HasMaxLength(2);
        builder.Property(translation => translation.SeoStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(translation => translation.SeoNotes).HasMaxLength(SeoProfile.NotesMaxLength);

        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(translation => translation.OpenGraphImageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(translation => new { translation.ContentItemId, translation.LanguageCode }).IsUnique();
        builder.HasIndex(translation => new { translation.LanguageCode, translation.SlugScope, translation.Slug }).IsUnique();
        builder.HasIndex(translation => new { translation.LanguageCode, translation.PrimaryKeywordNormalized }).HasFilter("primary_keyword_normalized IS NOT NULL");
        builder.HasIndex(translation => new { translation.LanguageCode, translation.MetaTitle }).HasFilter("meta_title IS NOT NULL");
    }
}
