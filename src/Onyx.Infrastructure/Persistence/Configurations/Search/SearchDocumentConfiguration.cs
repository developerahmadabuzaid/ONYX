using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Infrastructure.Search;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class SearchDocumentConfiguration : IEntityTypeConfiguration<SearchDocument>
{
    /// <summary>Text-search configuration per row language: PostgreSQL's built-in Snowball <c>arabic</c> and <c>english</c>.</summary>
    public const string ConfigurationSql = "CASE WHEN language_code = 'ar' THEN 'arabic'::regconfig ELSE 'english'::regconfig END";

    public void Configure(EntityTypeBuilder<SearchDocument> builder)
    {
        builder.ToTable("SearchDocuments");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id).ValueGeneratedNever();
        builder.Property(document => document.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(document => document.ContentType).HasMaxLength(40).IsRequired();
        builder.Property(document => document.GroupName).HasMaxLength(40).IsRequired();
        builder.Property(document => document.Title).HasMaxLength(SearchDocument.TitleMaxLength).IsRequired();
        builder.Property(document => document.Summary).HasMaxLength(SearchDocument.SummaryMaxLength).IsRequired();
        builder.Property(document => document.Path).HasMaxLength(SearchDocument.PathMaxLength).IsRequired();
        builder.Property(document => document.TitleNormalized).IsRequired();
        builder.Property(document => document.SummaryNormalized).IsRequired();
        builder.Property(document => document.BodyNormalized).IsRequired();
        builder.Property(document => document.NamesNormalized).IsRequired();

        builder.Property(document => document.SearchVector)
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                $"setweight(to_tsvector({ConfigurationSql}, title_normalized), 'A') || " +
                $"setweight(to_tsvector({ConfigurationSql}, summary_normalized), 'B') || " +
                $"setweight(to_tsvector({ConfigurationSql}, body_normalized), 'C')",
                stored: true);
        builder.Property(document => document.NamesVector)
            .HasColumnType("tsvector")
            .HasComputedColumnSql("to_tsvector('simple'::regconfig, names_normalized)", stored: true);

        builder.HasIndex(document => new { document.ContentItemId, document.LanguageCode }).IsUnique();
        builder.HasIndex(document => document.Path);
        builder.HasIndex(document => document.SearchVector).HasMethod("gin");
        builder.HasIndex(document => document.NamesVector).HasMethod("gin");
        builder.HasIndex(document => document.TitleNormalized).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}
