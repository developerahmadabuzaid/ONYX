using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Analytics;
using Onyx.Domain.Content;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

/// <summary>Daily search-performance aggregates; unique per provider, date, kind and dimension hash (idempotent re-syncs).</summary>
internal sealed class SearchPerformanceDailyMetricConfiguration : IEntityTypeConfiguration<SearchPerformanceDailyMetric>
{
    public void Configure(EntityTypeBuilder<SearchPerformanceDailyMetric> builder)
    {
        builder.ToTable("SearchPerformanceDailyMetrics");
        builder.HasKey(metric => metric.Id);
        builder.Property(metric => metric.Id).ValueGeneratedNever();
        builder.Property(metric => metric.ProviderKey).HasMaxLength(AnalyticsSyncState.ProviderKeyMaxLength).IsRequired();
        builder.Property(metric => metric.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(metric => metric.Page).HasMaxLength(AnalyticsDimensions.PathMaxLength).IsRequired();
        builder.Property(metric => metric.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(metric => metric.Query).HasMaxLength(AnalyticsDimensions.QueryMaxLength).IsRequired();
        builder.Property(metric => metric.Country).HasMaxLength(AnalyticsDimensions.ShortMaxLength).IsRequired();
        builder.Property(metric => metric.Device).HasMaxLength(SearchPerformanceDailyMetric.DeviceMaxLength).IsRequired();
        builder.Property(metric => metric.DimensionHash).HasMaxLength(AnalyticsDimensions.HashLength).IsFixedLength().IsRequired();

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(metric => metric.ContentItemId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(metric => new { metric.ProviderKey, metric.Date, metric.Kind, metric.DimensionHash }).IsUnique();
        builder.HasIndex(metric => new { metric.ProviderKey, metric.Kind, metric.Date });
        builder.HasIndex(metric => metric.ContentItemId).HasFilter("content_item_id IS NOT NULL");
    }
}
