using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.News;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class NewsSourceConfiguration : IEntityTypeConfiguration<NewsSource>
{
    public const string FeedUrlIndex = "ix_news_sources_feed_url";

    public void Configure(EntityTypeBuilder<NewsSource> builder)
    {
        builder.ToTable("NewsSources");
        builder.HasKey(source => source.Id);
        builder.Property(source => source.Id).ValueGeneratedNever();
        builder.Property(source => source.Name).HasMaxLength(NewsSource.NameMaxLength).IsRequired();
        builder.Property(source => source.HomepageUrl).HasMaxLength(NewsUrl.MaxLength).IsRequired();
        builder.Property(source => source.FeedUrl).HasMaxLength(NewsUrl.MaxLength).IsRequired();
        builder.Property(source => source.FeedFormat).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(source => source.LanguageCode).HasMaxLength(NewsSource.LanguageCodeMaxLength).IsRequired();
        builder.Property(source => source.Status).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(source => source.LicensingNote).HasMaxLength(NewsSource.LicensingNoteMaxLength);
        builder.Property(source => source.LastError).HasMaxLength(NewsSource.LastErrorMaxLength);
        builder.Property(source => source.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(source => source.IsAllowed);

        builder.HasOne<NewsCategory>().WithMany().HasForeignKey(source => source.DefaultCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(source => source.FeedUrl).IsUnique().HasDatabaseName(FeedUrlIndex);
        builder.HasIndex(source => new { source.Status, source.NextFetchAtUtc });
    }
}
