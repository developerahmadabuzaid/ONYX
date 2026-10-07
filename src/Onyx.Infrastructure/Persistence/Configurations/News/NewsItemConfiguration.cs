using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.News;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

/// <summary>Items: metadata and Onyx summaries only (no article text). The unique URL hash makes deduplication a database rule.</summary>
internal sealed class NewsItemConfiguration : IEntityTypeConfiguration<NewsItem>
{
    public const string UrlHashIndex = "ix_news_items_original_url_hash";

    public void Configure(EntityTypeBuilder<NewsItem> builder)
    {
        builder.ToTable("NewsItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.OriginalUrl).HasMaxLength(NewsUrl.MaxLength).IsRequired();
        builder.Property(item => item.OriginalUrlHash).HasMaxLength(NewsUrl.HashLength).IsFixedLength().IsRequired();
        builder.Property(item => item.OriginalTitle).HasMaxLength(NewsItem.OriginalTitleMaxLength).IsRequired();
        builder.Property(item => item.Publisher).HasMaxLength(NewsSource.NameMaxLength).IsRequired();
        builder.Property(item => item.SourceLanguageCode).HasMaxLength(NewsSource.LanguageCodeMaxLength).IsRequired();
        builder.Property(item => item.ClusterKey).HasMaxLength(NewsItem.ClusterKeyMaxLength).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(15).IsRequired();
        builder.Property(item => item.HoldReason).HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.SummaryOrigin).HasConversion<string>().HasMaxLength(15);
        builder.Property(item => item.SummaryProvider).HasMaxLength(NewsItem.ProviderMaxLength);
        builder.Property(item => item.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(item => item.IsPublic);

        builder.HasOne<NewsSource>().WithMany().HasForeignKey(item => item.SourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NewsCategory>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Translations).WithOne().HasForeignKey(translation => translation.NewsItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Translations).HasField("_translations").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.OriginalUrlHash).IsUnique().HasDatabaseName(UrlHashIndex);
        builder.HasIndex(item => new { item.Status, item.PublishedAtUtc });
        builder.HasIndex(item => new { item.SourceLanguageCode, item.ClusterKey, item.IngestedAtUtc });
        builder.HasIndex(item => new { item.SourceId, item.IngestedAtUtc });
    }
}
